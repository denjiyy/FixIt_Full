# TESTING_REPORT.md

End-to-end regression walk of the FixIt production deployment.

- **Target:** https://fixitfull-production.up.railway.app/
- **Walk date (UTC):** 2026-05-26
- **Method:** HTTP/HTML inspection via the harness `WebFetch` tool. **The
  Section 1 specification calls for an Atlas-style browser agent (DOM
  interaction, console-error capture, 1440 px and 390 px viewport
  measurement, map-pin placement, lightbox drag, NDJSON streaming
  verification). No live browser-automation tool was loaded in this
  session — only HTTP + parsed HTML.** Checks that are not verifiable
  without a real browser are explicitly listed under "Browser-only gaps"
  per page and at the bottom of this report.
- **Baseline:** The walk was performed against the deployment as it was
  before the changes in this PR landed. Items the PR adds (the `/Search`
  page, lowercase footer `/health/reports` cleanup, navbar quiet-admin
  removal, `is-active` server-side state, `bi-list ↔ bi-x` toggle,
  `scroll-margin-top: 6rem`) are listed in §3.

## 1. Public happy-path pages (HTTP / HTML)

| # | Page | URL | HTTP | Server-rendered checks |
|---|------|-----|------|------------------------|
| a | Home | `/` | 200 | Hero ("Report & Fix Community Issues"), KPI counters (0% resolution, 5 cities, 9 active issues), 3 featured issue cards. No raw `null`/`undefined`, no broken `src`. |
| b1 | Issues list | `/issues` | 200 | 9 issue cards rendered, status/priority/sort filter toolbar present, AI natural-language filter input present. Single-page result so pagination not visible. |
| b2 | Cities | `/Cities` | 200 | 9 Bulgarian city cards (page 1/3), country chip filter rendered. |
| b3 | Leaderboards | `/Leaderboards` | 200 | Weekly/Monthly/All-Time tabs present, each currently showing "No entries yet" (empty-state copy). |
| b4 | Safety dropdown → Hazard Mode | `/safety/hazard-mode` | 200 | Hazard map container, sidebar copy "Live hazard reporting across Ruse", category chips, severity legend, "0 Total hazards" empty state, AI trend-insight card ("Select a hazard to generate a cluster insight"). |
| b6 | Analytics dropdown → Health Reports | `/HealthReports` | 200 | Community Health Score dashboard rendered (score 55.0), issue priority breakdown, trending infra-problems table. No explicit city-selector dropdown surfaced in the HTML response — see §3 follow-up. |
| j | Cities (heatmap launching point) | `/Cities` | 200 | See b2. The "View Heatmaps" navbar item points at `/Cities` rather than a dedicated heatmap route. |
| l | Issue detail | `/issues/6a02322515134d03cabaa644` | 200 | Title, body, location coordinates, AI analysis card with category + severity, comment section (empty), vote widget (9 ▲). |
| m | Admin sign-in | `/admin/login` | 200 | "Operator sign in" form with username + password, gated by role. |
| n1 | Identity sign-in | `/Identity/Account/Login` | 200 | Email + password + "Keep me signed in" + Forgot password + Google SSO. |
| n2 | Identity register | `/Identity/Account/Register` | 200 | Email + display name + password + confirm + Google SSO. |

## 2. Pages that 404'd (root-cause + remediation)

| Probed URL | Symptom | Root cause | Remediation in this PR |
|------------|---------|------------|------------------------|
| `/Safety/HazardMode` (PascalCase) | 404 | The page is declared with an explicit lowercase route `@page "/safety/hazard-mode"`. Direct PascalCase deep-links 404 because routing is case-sensitive. The navbar uses `asp-page="/Safety/HazardMode"` which Razor resolves to the lowercase URL, so users following the nav don't hit this. | Documented. No code change needed — both Safety dropdown items go through `asp-page` and resolve correctly. |
| `/Heatmaps` | 404 | No `Pages/Heatmaps/Index.cshtml` exists; the navbar Analytics dropdown's "View Heatmaps" item points at `/Cities` and city detail renders the heatmap. | Documented; no dead link present in `_Layout.cshtml`. |
| `/Search` | 404 | Spec §3.1 dead link — the production NotFound page links to `/search` but no Search page existed. | **Added** `FixIt/Pages/Search.cshtml` + `.cshtml.cs`. Page accepts `?q=`, calls `IIssueService.GetAllIssuesAsync(searchQuery)`, renders `_IssueCard` grid with `issues-shared.css` for styling. |
| `/health/reports` (referenced from `NotFound.cshtml`) | 404 (would 404 on click) | Hard-coded path mismatch — actual route is `/HealthReports`. | **Replaced** `/health/reports` → `/HealthReports` in `NotFound.cshtml` (and `/search` → `/Search`). Footer links already use `asp-page="/HealthReports/Index"` so they resolve correctly. |
| `/Identity/Account/Manage/ChangePassword`, `/Identity/Account/Manage/TwoFactorAuthentication` (linked from `Manage.cshtml` + `ConnectedAccounts.cshtml`) | Would 404 (no Identity Manage subdirectory scaffolded) | Identity Manage area was never scaffolded. | **Replaced** target hrefs with `/Settings/Index` (which exists) and annotated with `TODO` comments. Spec §3.3 explicitly authorises this fallback when scaffolding "is not feasible". |

## 3. Changes this PR introduces (verification deferred to post-deploy)

Each item below is implemented locally and will be exercised on the next Railway deploy.

### 3.1 Navigation (Section 2)
- Bootstrap dropdown markup was already correct (`data-bs-toggle="dropdown"`); kept.
- Active route highlighting is now **server-rendered** in `_Layout.cshtml`'s `@{}` block via `isActive(...)` / `anyActive(...)` helpers, applying `is-active` and `aria-current="page"` to anchors and dropdown toggles. The client-side `initActiveNavigation()` in `site.js` was removed (eliminates flash-of-wrong-state).
- Dropdown parents (`Safety`, `Analytics`) get `is-active` when any child route matches.
- The unauthenticated quiet `/admin/login` link in the action bar was **removed** (it leaked implementation detail). The Admin Panel link inside the authenticated user dropdown remains for `Admin`/`Moderator`.
- `initMobileNavToggle()` added to `site.js`: hooks `show.bs.collapse` / `hide.bs.collapse` to swap `bi-list ↔ bi-x` and toggle `body.nav-open-lock { overflow: hidden }`.
- `section[id] { scroll-margin-top: 6rem; }` added to `site.css` so anchor-target sections aren't covered by the sticky header.

### 3.2 Routing audit (Section 3)
- `/Search` page added (§2 above).
- `/health/reports` → `/HealthReports` in `NotFound.cshtml`.
- Footer links audited — already use `asp-page` (correct).
- Tag-link guard: `IssueDetailModel` now injects `ITagService`, validates AI-suggested tags via `GetTagByNameAsync`, exposes `ValidSuggestedTagSet`, and `Detail.cshtml` only renders a `/tags/{tag}` link if the tag is in the set; otherwise renders an inert chip.
- User profile link already includes `userId` (was already in HEAD via `asp-route-userId`).
- `[data-auto-submit="change"]` handler was already wired in `site.js`.
- Admin area pages (`Dashboard/Cities/Issues/Users/Reports/Login`) already scaffolded; no stubbing required.

### 3.3 Refactor (Section 4)
- `HealthReportService.CalculateHealthScore` switched from `100 - openIssues*5` (broken: any 20 open issues floored to 0) to `100 * (1 - open/total)` per spec. Existing test updated.
- `AdminSuggestionsService.AnalyzeUserBehavior` dead `IsBanned` branch removed; documented why confidence floors below 50.
- `OpenAIIssueAnalysisService.ParseAnalysisResponse` now attempts `JsonDocument.Parse` (with code-fence stripping) before falling back to the regex extractor.
- All interpolated `_logger.Log*($"...")` calls in `OpenAIIssueAnalysisService`, `AdminSuggestionsService`, `SuggestionsController`, `AnalysisController`, `TranslationService`, `HealthReportGenerationService`, `LeaderboardRegenerationService` converted to structured logging.
- `GeocodingController` rewritten: static `HttpClient` replaced with `IHttpClientFactory` (named client `Nominatim` registered in `ServicesExtensions`), `Dictionary` + `SemaphoreSlim` replaced with `ConcurrentDictionary` + semaphore on eviction only, unused `ExtractCityFromAddressString` deleted, structured logging throughout.
- `TagsController.GetAllTags` no longer makes the dead `GetAllTagsAsync(1,1)` second call; `CountAllTags()` stub deleted; `ITagService.CountAllTagsAsync()` added and wired to `_tagRepo.CountAsync(t => t.IsApproved)`.
- `IssueService`: `FIXME` comment added on the `Expression.AndAlso/Invoke` predicate composition (fragile but functional); `GetIssuesByCityAsync(string)` non-paginated overload now has an XML `<remarks>` doc warning callers off hot paths.
- CSS: 476 lines of duplicated `.issue-page*` / `.issue-card*` / status-badge rules removed from `home-index.css`. `Index.cshtml` now loads `issues-shared.css` (Issues list page already did).
- `Program.cs` reduced from 1228 → ~360 lines. DI extracted into `FixIt/Extensions/`:
  - `MongoDbExtensions.AddMongoDb` — Mongo client / db / migrations / repositories
  - `IdentityExtensions.AddIdentityWithMongo` — Identity + claims factory + authorization policies
  - `AuthExtensions.AddFixItAuthentication` — Cookie + Google OAuth + JWT
  - `ServicesExtensions.AddFixItBusinessServices` — business services + typed/named HTTP clients + email + audit + background services
  - `InfrastructureExtensions` — rate limiting / Cloudinary / CORS / output-cache + compression + data protection + forwarded headers
  - Startup diagnostics, DB seeding, and the middleware pipeline remain inline in `Program.cs` per the spec's operational-clarity rule.

## 4. Browser-only gaps (NOT verified by this walk)

The following checks from Section 1 of the spec require a real browser
(DOM events, layout reflow, console capture). They are NOT verified
here; the codebase changes for each are described in §3 and should be
re-verified once Railway deploys this branch.

- (a) Home page: console errors at runtime, image lazy-load behaviour at 1440 px and 390 px viewports.
- (b) Every nav-link "resolves to a page with HTTP 200 and non-empty body" — partially verified by §1 above; the `is-active` highlight on the *current* route was not verified visually.
- (c) "Apply each filter combination (status × priority × sort), assert results change and pagination works." — not exercisable without DOM interaction.
- (d) Issue create flow: map pin placement, tag autocomplete, AI draft suggestion panel, media upload, redirect-to-detail. Requires auth + DOM.
- (e) Issue detail: AI analysis card render (verified via HTML), voting buttons (require auth), comment submission (requires auth), lightbox-on-image-click, Leaflet marker visibility.
- (f) Hazard Mode: map load, sidebar populate, quick-report modal on map click, AI cluster insight streaming.
- (g) Safety Map: marker rendering, AI insight card updates on marker click.
- (h) Health Reports / Heatmaps: chart rendering, city selector dropdown behaviour.
- (i) Leaderboard tabs (Weekly/Monthly/All-Time) — server-rendered HTML present and showing empty state.
- (j) Cities: country chip filter behaviour, city focus issue feed (requires nav).
- (k) Settings (Privacy, Email Preferences, Connected Accounts) — require auth.
- (l) User Profile — requires auth.
- (m, n) Auth pages render correctly — §1 verified the HTML; client-side validation requires DOM.
- Layout breaks at **1440 px (desktop) and 390 px (mobile-viewport)** — not measurable without a browser. The relevant CSS was inspected; `app-navbar` collapses at `xl` breakpoint, `app-actions` stacks under 75 rem, and `app-footer__grid` collapses to single-column under 62 rem.

## 5. Recommendation for the next walk

Once this PR deploys to Railway, re-run a real Atlas browser walk that
covers the §4 items above. Specifically watch for:

1. JS console — should be clean. The removal of `initActiveNavigation()`
   removes a `querySelectorAll('.app-nav-link, .admin-nav__link')` pass,
   which on stale caches might still try to add `is-active`; the
   server-rendered class handles this so the duplicate is harmless.
2. Mobile menu: clicking the hamburger should swap `bi-list ↔ bi-x` and
   prevent `<body>` scrolling while the collapse is open.
3. The `/Search?q=...` page should now return 200 with results (or the
   "No results" state).
4. Tag chips on issue-detail should only be linkable for tags that exist
   in the tag store; AI-suggested tags that aren't in the DB render as
   inert chips.

End of report.
