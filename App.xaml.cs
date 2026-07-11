using WallPanelPlanner.Drawing;
using WallPanelPlanner.Persistence;
using WallPanelPlanner.Services;
using WallPanelPlanner.ViewModels;
using SQLitePCL;

namespace WallPanelPlanner;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        Batteries_V2.Init();

        SqliteDatabaseFactory = new SqliteDatabaseFactory();
        RoomRepository = new SqliteRoomRepository(SqliteDatabaseFactory);
        WallRepository = new SqliteWallRepository(SqliteDatabaseFactory);
        CircuitRepository = new SqliteCircuitRepository(SqliteDatabaseFactory);
        HomeStateService = new HomeStateService(RoomRepository, WallRepository, CircuitRepository);
        GymSetupService = new GymSetupService();
        GymSetupEditorStateService = new GymSetupEditorStateService();
        GymSetupPageStateService = new GymSetupPageStateService(GymSetupEditorStateService);
        HoldAnalysisSuggestionService = new HoldAnalysisSuggestionService();
        CircuitEditingService = new CircuitEditingService();
        CircuitPageStateService = new CircuitPageStateService();
        WallConfigurationStorageService = new WallConfigurationStorageService(WallRepository);
        WallImageService = new WallImageService();
        PanelImageAlignmentService = new PanelImageAlignmentService();
        Esp32SettingsService = new Esp32SettingsService();
        Esp32PayloadBuilderService = new Esp32PayloadBuilderService();
        Esp32ApiClient = new Esp32ApiClient();
        GymSetupViewModel = new GymSetupViewModel(GymSetupService, WallConfigurationStorageService, WallRepository, RoomRepository);
        CircuitEditorViewModel = new CircuitEditorViewModel(CircuitEditingService, GymSetupViewModel, CircuitRepository);
        LayoutPreviewDrawable = new LayoutPreviewDrawable();
    }

    public ISqliteDatabaseFactory SqliteDatabaseFactory { get; }

    public IRoomRepository RoomRepository { get; }

    public IWallRepository WallRepository { get; }

    public ICircuitRepository CircuitRepository { get; }

    public IHomeStateService HomeStateService { get; }

    public IGymSetupService GymSetupService { get; }

    public IGymSetupEditorStateService GymSetupEditorStateService { get; }

    public IGymSetupPageStateService GymSetupPageStateService { get; }

    public IHoldAnalysisSuggestionService HoldAnalysisSuggestionService { get; }

    public ICircuitEditingService CircuitEditingService { get; }

    public ICircuitPageStateService CircuitPageStateService { get; }

    public IWallConfigurationStorageService WallConfigurationStorageService { get; }

    public IWallImageService WallImageService { get; }

    public IPanelImageAlignmentService PanelImageAlignmentService { get; }

    public IEsp32SettingsService Esp32SettingsService { get; }

    public IEsp32PayloadBuilderService Esp32PayloadBuilderService { get; }

    public IEsp32ApiClient Esp32ApiClient { get; }

    public GymSetupViewModel GymSetupViewModel { get; }


    public CircuitEditorViewModel CircuitEditorViewModel { get; }

    public LayoutPreviewDrawable LayoutPreviewDrawable { get; }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
