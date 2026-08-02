namespace RouteLab;

public partial class AppShell : Shell
{
	private IDisposable? navigationBusyScope;

	public AppShell()
	{
		InitializeComponent();

		if (DeviceInfo.Platform == DevicePlatform.WinUI)
		{
			FlyoutBehavior = FlyoutBehavior.Locked;
		}

		Routing.RegisterRoute("walls-page", typeof(WallsPage));
		Routing.RegisterRoute("new-wall-page", typeof(NewWallPage));
		Routing.RegisterRoute("gym-setup-page", typeof(GymSetupPage));
		Routing.RegisterRoute("hardware-mapping-page", typeof(HardwareMappingPage));
		Routing.RegisterRoute("panel-detail-page", typeof(PanelDetailPage));
		Routing.RegisterRoute("panel-hole-grid-editor-page", typeof(PanelHoleGridEditorPage));
		Routing.RegisterRoute("panel-image-page", typeof(PanelImagePage));
		Routing.RegisterRoute("biomechanical-profiles-page", typeof(BiomechanicalProfilesPage));
		Routing.RegisterRoute("next-hold-suggestion-page", typeof(NextHoldSuggestionPage));

		Navigating += OnShellNavigating;
		Navigated += OnShellNavigated;
	}

	private void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
	{
		navigationBusyScope ??= ((App)Application.Current!).BusyIndicatorService.Show("Apertura pagina...");
	}

	private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
	{
		navigationBusyScope?.Dispose();
		navigationBusyScope = null;
	}
}
