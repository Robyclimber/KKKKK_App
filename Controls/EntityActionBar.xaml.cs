namespace RouteLab.Controls;

public partial class EntityActionBar : ContentView
{
    public static readonly BindableProperty ContextTextProperty = BindableProperty.Create(
        nameof(ContextText),
        typeof(string),
        typeof(EntityActionBar),
        "Azioni");

    public static readonly BindableProperty ShowAddProperty = BindableProperty.Create(
        nameof(ShowAdd),
        typeof(bool),
        typeof(EntityActionBar),
        true);

    public static readonly BindableProperty ShowSaveProperty = BindableProperty.Create(
        nameof(ShowSave),
        typeof(bool),
        typeof(EntityActionBar),
        true);

    public static readonly BindableProperty ShowDeleteProperty = BindableProperty.Create(
        nameof(ShowDelete),
        typeof(bool),
        typeof(EntityActionBar),
        true);

    public static readonly BindableProperty CanAddProperty = BindableProperty.Create(
        nameof(CanAdd),
        typeof(bool),
        typeof(EntityActionBar),
        true);

    public static readonly BindableProperty CanSaveProperty = BindableProperty.Create(
        nameof(CanSave),
        typeof(bool),
        typeof(EntityActionBar),
        true);

    public static readonly BindableProperty CanDeleteProperty = BindableProperty.Create(
        nameof(CanDelete),
        typeof(bool),
        typeof(EntityActionBar),
        true);

    public static readonly BindableProperty AddDescriptionProperty = BindableProperty.Create(
        nameof(AddDescription),
        typeof(string),
        typeof(EntityActionBar),
        "Aggiungi");

    public static readonly BindableProperty SaveDescriptionProperty = BindableProperty.Create(
        nameof(SaveDescription),
        typeof(string),
        typeof(EntityActionBar),
        "Salva");

    public static readonly BindableProperty DeleteDescriptionProperty = BindableProperty.Create(
        nameof(DeleteDescription),
        typeof(string),
        typeof(EntityActionBar),
        "Elimina");

    public EntityActionBar()
    {
        InitializeComponent();
    }

    public event EventHandler? AddClicked;

    public event EventHandler? SaveClicked;

    public event EventHandler? DeleteClicked;

    public string ContextText
    {
        get => (string)GetValue(ContextTextProperty);
        set => SetValue(ContextTextProperty, value);
    }

    public bool ShowAdd
    {
        get => (bool)GetValue(ShowAddProperty);
        set => SetValue(ShowAddProperty, value);
    }

    public bool ShowSave
    {
        get => (bool)GetValue(ShowSaveProperty);
        set => SetValue(ShowSaveProperty, value);
    }

    public bool ShowDelete
    {
        get => (bool)GetValue(ShowDeleteProperty);
        set => SetValue(ShowDeleteProperty, value);
    }

    public bool CanAdd
    {
        get => (bool)GetValue(CanAddProperty);
        set => SetValue(CanAddProperty, value);
    }

    public bool CanSave
    {
        get => (bool)GetValue(CanSaveProperty);
        set => SetValue(CanSaveProperty, value);
    }

    public bool CanDelete
    {
        get => (bool)GetValue(CanDeleteProperty);
        set => SetValue(CanDeleteProperty, value);
    }

    public string AddDescription
    {
        get => (string)GetValue(AddDescriptionProperty);
        set => SetValue(AddDescriptionProperty, value);
    }

    public string SaveDescription
    {
        get => (string)GetValue(SaveDescriptionProperty);
        set => SetValue(SaveDescriptionProperty, value);
    }

    public string DeleteDescription
    {
        get => (string)GetValue(DeleteDescriptionProperty);
        set => SetValue(DeleteDescriptionProperty, value);
    }

    private void OnAddClicked(object? sender, EventArgs e)
    {
        AddClicked?.Invoke(this, e);
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        SaveClicked?.Invoke(this, e);
    }

    private void OnDeleteClicked(object? sender, EventArgs e)
    {
        DeleteClicked?.Invoke(this, e);
    }
}
