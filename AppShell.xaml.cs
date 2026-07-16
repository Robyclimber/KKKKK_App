namespace RuoteLab;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute("walls-page", typeof(WallsPage));
		Routing.RegisterRoute("gym-setup-page", typeof(GymSetupPage));
		Routing.RegisterRoute("hardware-mapping-page", typeof(HardwareMappingPage));
		Routing.RegisterRoute("panel-image-page", typeof(PanelImagePage));
	}
}
