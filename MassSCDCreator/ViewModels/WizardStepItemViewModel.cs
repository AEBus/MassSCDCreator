using CommunityToolkit.Mvvm.ComponentModel;

namespace MassSCDCreator.ViewModels;

public sealed partial class WizardStepItemViewModel : ObservableObject {
    public required WizardStep Step { get; init; }
    public required int Number { get; init; }
    public required string TitleKey { get; init; }
    public required string DescriptionKey { get; init; }

    [ObservableProperty]
    private string displayTitle = string.Empty;

    [ObservableProperty]
    private string displayDescription = string.Empty;

    [ObservableProperty]
    private bool isVisible = true;

    [ObservableProperty]
    private bool isCurrent;

    [ObservableProperty]
    private bool isCompleted;
}
