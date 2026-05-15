using System.ComponentModel;
using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Views;

public partial class ReportIssuePage : ContentPage
{
    public ReportIssuePage(ReportIssueViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnBindingContextChanged()
    {
        if (BindingContext is ReportIssueViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        base.OnBindingContextChanged();

        if (BindingContext is ReportIssueViewModel newViewModel)
        {
            newViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is ReportIssueViewModel viewModel)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            await viewModel.OnAppearingAsync();
        }

        FormRoot.TranslationY = 25;
        FormRoot.Opacity = 0;

        await Task.WhenAll(
            FormRoot.FadeTo(1, 350, Easing.CubicOut),
            FormRoot.TranslateTo(0, 0, 350, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is ReportIssueViewModel viewModel)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel.OnDisappearing();
        }

        base.OnDisappearing();
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReportIssueViewModel.HasPhoto) &&
            BindingContext is ReportIssueViewModel { HasPhoto: true })
        {
            await PulsePhotoBorderAsync();
            await PhotoPreview.ScaleTo(1.05, 150, Easing.CubicOut);
            await PhotoPreview.ScaleTo(1.0, 150, Easing.CubicOut);
        }

        if (e.PropertyName == nameof(ReportIssueViewModel.SubmissionSucceeded) &&
            BindingContext is ReportIssueViewModel { SubmissionSucceeded: true })
        {
            SuccessCheckLabel.IsVisible = true;
            await Task.WhenAll(
                SuccessCheckLabel.FadeTo(1, 120, Easing.CubicOut),
                SuccessCheckLabel.ScaleTo(1.1, 220, Easing.SpringOut));
            await SuccessCheckLabel.ScaleTo(1.0, 120, Easing.CubicOut);
        }
    }

    private async Task PulsePhotoBorderAsync()
    {
        var original = (Brush)Application.Current!.Resources["Surface2"];
        var primary = (Brush)Application.Current.Resources["Primary"];
        PhotoBorder.Stroke = primary;
        await Task.Delay(300);
        PhotoBorder.Stroke = original;
    }
}
