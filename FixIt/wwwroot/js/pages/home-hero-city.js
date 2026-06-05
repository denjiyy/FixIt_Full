// Home hero: swap the placeholder building icon for a photo of the visitor's
// city, derived from their geolocation. Entirely progressive — if geolocation
// is denied/unavailable or any lookup fails, the gradient + icon stay as-is.
//
// Pipeline (all keyless, CORS-enabled):
//   1. navigator.geolocation -> { latitude, longitude }
//   2. BigDataCloud reverse-geocode -> city name
//   3. Wikipedia page image -> photo URL
// The geocode + Wikipedia hosts must be whitelisted in the CSP `connect-src`.
(function () {
    'use strict';

    var el = document.getElementById('heroCityPhoto');
    if (!el || !navigator.geolocation) { return; }

    var imgLayer = el.querySelector('.lhero__photo-img');
    var cityWrap = el.querySelector('.lhero__photo-city');
    var cityName = el.querySelector('.lhero__photo-city-name');

    navigator.geolocation.getCurrentPosition(onPosition, function () { /* keep fallback */ }, {
        enableHighAccuracy: false,
        timeout: 8000,
        maximumAge: 10 * 60 * 1000
    });

    function onPosition(pos) {
        reverseGeocode(pos.coords.latitude, pos.coords.longitude)
            .then(function (city) {
                if (!city) { return; }
                showCity(city);
                return cityImage(city).then(function (url) {
                    if (url) { preload(url); }
                });
            })
            .catch(function () { /* keep fallback */ });
    }

    function showCity(name) {
        cityName.textContent = name;
        cityWrap.hidden = false;
    }

    function preload(url) {
        var img = new Image();
        img.onload = function () {
            imgLayer.style.backgroundImage = 'url("' + url + '")';
            el.dataset.state = 'loaded';
        };
        img.src = url;
    }

    function reverseGeocode(lat, lon) {
        var url = 'https://api.bigdatacloud.net/data/reverse-geocode-client'
            + '?latitude=' + encodeURIComponent(lat)
            + '&longitude=' + encodeURIComponent(lon)
            + '&localityLanguage=en';
        return fetch(url)
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (d) {
                if (!d) { return null; }
                return d.city || d.locality || d.principalSubdivision || null;
            });
    }

    function cityImage(city) {
        var url = 'https://en.wikipedia.org/w/api.php?action=query&format=json&origin=*'
            + '&prop=pageimages&piprop=thumbnail&pithumbsize=900'
            + '&titles=' + encodeURIComponent(city);
        return fetch(url)
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (d) {
                var pages = d && d.query && d.query.pages;
                if (!pages) { return null; }
                for (var key in pages) {
                    if (pages[key].thumbnail && pages[key].thumbnail.source) {
                        return pages[key].thumbnail.source;
                    }
                }
                return null;
            });
    }
})();
