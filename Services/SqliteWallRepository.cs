using SQLite;
using RouteLab.Models;
using RouteLab.Persistence;
using RouteLab.Persistence.Entities;

namespace RouteLab.Services;

public sealed class SqliteWallRepository : IWallRepository
{
    private readonly ISqliteDatabaseFactory databaseFactory;
    private readonly IBusyIndicatorService busyIndicatorService;

    public SqliteWallRepository(
        ISqliteDatabaseFactory databaseFactory,
        IBusyIndicatorService busyIndicatorService)
    {
        this.databaseFactory = databaseFactory;
        this.busyIndicatorService = busyIndicatorService;
    }

    public async Task<int> SaveAsync(WallDefinition wall, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wall);
        return await busyIndicatorService.RunAsync("Salvataggio parete...", async () =>
        {
            wall.AutoAssignLedIndicesByWallRouting();

            var connection = await databaseFactory.GetConnectionAsync();
            var roomName = string.IsNullOrWhiteSpace(wall.RoomName) ? "Sala Arrampicata" : wall.RoomName;
            var existingWall = wall.Id > 0
                ? await connection.Table<WallEntity>()
                    .Where(entity => entity.Id == wall.Id)
                    .FirstOrDefaultAsync()
                : await connection.Table<WallEntity>()
                    .Where(entity => entity.RoomName == roomName && entity.Name == wall.Name)
                    .FirstOrDefaultAsync();

            var entity = existingWall ?? new WallEntity();
            entity.RoomName = string.IsNullOrWhiteSpace(wall.RoomName) ? "Sala Arrampicata" : wall.RoomName;
            entity.Name = wall.Name;
            entity.Width = wall.Width;
            entity.Height = wall.Height;
            entity.ImagePath = wall.ImagePath;
            entity.ImageOffsetX = wall.ImageOffsetX;
            entity.ImageOffsetY = wall.ImageOffsetY;
            entity.ImageScale = wall.ImageScale;
            entity.ImageOpacity = wall.ImageOpacity;
            entity.LedVerticalDirection = (int)wall.LedVerticalDirection;
            entity.UpdatedAtUtcTicks = DateTime.UtcNow.Ticks;

            if (entity.Id == 0)
            {
                await connection.InsertAsync(entity);
            }
            else
            {
                await connection.UpdateAsync(entity);
                await connection.Table<PanelEntity>().DeleteAsync(panel => panel.WallId == entity.Id);
                await connection.Table<WallHoleEntity>().DeleteAsync(hole => hole.WallId == entity.Id);
            }

            foreach (var panel in wall.Panels)
            {
                await connection.InsertAsync(new PanelEntity
                {
                    WallId = entity.Id,
                    Name = panel.Name,
                    X = panel.X,
                    Y = panel.Y,
                    Width = panel.Width,
                    Height = panel.Height,
                    HorizontalSpacing = panel.HorizontalSpacing,
                    VerticalSpacing = panel.VerticalSpacing,
                    EdgeOffsetX = panel.EdgeOffsetX,
                    EdgeOffsetY = panel.EdgeOffsetY,
                    LedRoutingAxis = (int)panel.LedRoutingAxis,
                    LedStartDirection = (int)panel.LedStartDirection,
                    ImagePath = panel.ImagePath,
                    ImageSourcePath = panel.ImageSourcePath,
                    IsImageRectified = panel.IsImageRectified,
                    ImageOffsetX = panel.ImageOffsetX,
                    ImageOffsetY = panel.ImageOffsetY,
                    ImageScale = panel.ImageScale,
                    ImageOpacity = panel.ImageOpacity,
                    ImageCropLeft = panel.ImageCropLeft,
                    ImageCropTop = panel.ImageCropTop,
                    ImageCropRight = panel.ImageCropRight,
                    ImageCropBottom = panel.ImageCropBottom,
                    ImagePerspectiveTopLeftX = panel.ImagePerspectiveTopLeftX,
                    ImagePerspectiveTopLeftY = panel.ImagePerspectiveTopLeftY,
                    ImagePerspectiveTopRightX = panel.ImagePerspectiveTopRightX,
                    ImagePerspectiveTopRightY = panel.ImagePerspectiveTopRightY,
                    ImagePerspectiveBottomLeftX = panel.ImagePerspectiveBottomLeftX,
                    ImagePerspectiveBottomLeftY = panel.ImagePerspectiveBottomLeftY,
                    ImagePerspectiveBottomRightX = panel.ImagePerspectiveBottomRightX,
                    ImagePerspectiveBottomRightY = panel.ImagePerspectiveBottomRightY
                });
            }

            if (wall.HoleLayout.Count == 0)
            {
                wall.RegenerateHoleLayoutFromPanels();
            }

            foreach (var hole in wall.GetOrderedHoles())
            {
                await connection.InsertAsync(new WallHoleEntity
                {
                    WallId = entity.Id,
                    PanelName = hole.PanelName,
                    PanelX = hole.PanelX,
                    PanelY = hole.PanelY,
                    RelativeX = hole.RelativeX,
                    RelativeY = hole.RelativeY,
                    AbsoluteX = hole.AbsoluteX,
                    AbsoluteY = hole.AbsoluteY,
                    PointId = hole.PointId,
                    LedIndex = hole.LedIndex,
                    IsEnabled = hole.IsEnabled,
                    HasHold = hole.HasHold,
                    HoldSize = (int)hole.HoldSize,
                    HoldType = (int)hole.HoldType,
                    HasEstimatedHoldMetadata = hole.HasEstimatedHoldMetadata,
                    SourceKind = (int)hole.SourceKind
                });
            }

            wall.Id = entity.Id;
            return entity.Id;
        });
    }

    public async Task SaveHoleAsync(
        WallDefinition wall,
        WallHoleDefinition hole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wall);
        cancellationToken.ThrowIfCancellationRequested();
        await busyIndicatorService.RunAsync("Salvataggio presa...", async () =>
        {
            if (wall.Id <= 0)
            {
                throw new InvalidOperationException("Salva prima la parete.");
            }

            var connection = await databaseFactory.GetConnectionAsync();
            var entity = await connection.Table<WallHoleEntity>()
                .Where(current =>
                    current.WallId == wall.Id &&
                    current.PanelName == hole.PanelName &&
                    current.RelativeX == hole.RelativeX &&
                    current.RelativeY == hole.RelativeY)
                .OrderByDescending(current => current.Id)
                .FirstOrDefaultAsync();

            if (entity is null)
            {
                throw new InvalidOperationException("Il foro non e' ancora presente nel database. Salva prima il pannello.");
            }

            entity.HasHold = hole.HasHold;
            entity.HoldSize = (int)hole.HoldSize;
            entity.HoldType = (int)hole.HoldType;
            entity.HasEstimatedHoldMetadata = hole.HasEstimatedHoldMetadata;
            await connection.UpdateAsync(entity);
        });
    }

    public async Task<IReadOnlyList<WallDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await busyIndicatorService.RunAsync("Caricamento pareti...", async () =>
        {
            var connection = await databaseFactory.GetConnectionAsync();
            var walls = await connection.Table<WallEntity>().OrderBy(entity => entity.Name).ToListAsync();
            var panels = await connection.Table<PanelEntity>().ToListAsync();
            var holes = await connection.Table<WallHoleEntity>().ToListAsync();

            var result = new List<WallDefinition>(walls.Count);
            foreach (var wallEntity in walls)
            {
                var wall = new WallDefinition
                {
                    Id = wallEntity.Id,
                    RoomName = string.IsNullOrWhiteSpace(wallEntity.RoomName) ? "Sala Arrampicata" : wallEntity.RoomName,
                    Name = wallEntity.Name,
                    Width = wallEntity.Width,
                    Height = wallEntity.Height,
                    ImagePath = wallEntity.ImagePath,
                    ImageOffsetX = wallEntity.ImageOffsetX,
                    ImageOffsetY = wallEntity.ImageOffsetY,
                    ImageScale = wallEntity.ImageScale,
                    ImageOpacity = wallEntity.ImageOpacity,
                    LedVerticalDirection = wallEntity.LedVerticalDirection is
                        (int)LedStartDirection.TopToBottom or
                        (int)LedStartDirection.BottomToTop
                            ? (LedStartDirection)wallEntity.LedVerticalDirection
                            : LedStartDirection.TopToBottom
                };

                foreach (var panelEntity in panels.Where(panel => panel.WallId == wallEntity.Id))
                {
                    wall.Panels.Add(new PanelDefinition
                    {
                        Name = panelEntity.Name,
                        X = panelEntity.X,
                        Y = panelEntity.Y,
                        Width = panelEntity.Width,
                        Height = panelEntity.Height,
                        HorizontalSpacing = panelEntity.HorizontalSpacing,
                        VerticalSpacing = panelEntity.VerticalSpacing,
                        EdgeOffsetX = panelEntity.EdgeOffsetX,
                        EdgeOffsetY = panelEntity.EdgeOffsetY,
                        LedRoutingAxis = Enum.IsDefined(typeof(LedRoutingAxis), panelEntity.LedRoutingAxis)
                            ? (LedRoutingAxis)panelEntity.LedRoutingAxis
                            : LedRoutingAxis.Vertical,
                        LedStartDirection = Enum.IsDefined(typeof(LedStartDirection), panelEntity.LedStartDirection)
                            ? (LedStartDirection)panelEntity.LedStartDirection
                            : LedStartDirection.BottomToTop,
                        ImagePath = panelEntity.ImagePath,
                        ImageSourcePath = panelEntity.ImageSourcePath ?? panelEntity.ImagePath,
                        IsImageRectified = panelEntity.IsImageRectified,
                        ImageOffsetX = panelEntity.ImageOffsetX,
                        ImageOffsetY = panelEntity.ImageOffsetY,
                        ImageScale = panelEntity.ImageScale <= 0 ? 1d : panelEntity.ImageScale,
                        ImageOpacity = panelEntity.ImageOpacity <= 0 ? 0.55d : panelEntity.ImageOpacity,
                        ImageCropLeft = panelEntity.ImageCropLeft,
                        ImageCropTop = panelEntity.ImageCropTop,
                        ImageCropRight = panelEntity.ImageCropRight,
                        ImageCropBottom = panelEntity.ImageCropBottom,
                        ImagePerspectiveTopLeftX = panelEntity.ImagePerspectiveTopLeftX,
                        ImagePerspectiveTopLeftY = panelEntity.ImagePerspectiveTopLeftY,
                        ImagePerspectiveTopRightX = panelEntity.ImagePerspectiveTopRightX <= 0 ? 1d : panelEntity.ImagePerspectiveTopRightX,
                        ImagePerspectiveTopRightY = panelEntity.ImagePerspectiveTopRightY,
                        ImagePerspectiveBottomLeftX = panelEntity.ImagePerspectiveBottomLeftX,
                        ImagePerspectiveBottomLeftY = panelEntity.ImagePerspectiveBottomLeftY <= 0 ? 1d : panelEntity.ImagePerspectiveBottomLeftY,
                        ImagePerspectiveBottomRightX = panelEntity.ImagePerspectiveBottomRightX <= 0 ? 1d : panelEntity.ImagePerspectiveBottomRightX,
                        ImagePerspectiveBottomRightY = panelEntity.ImagePerspectiveBottomRightY <= 0 ? 1d : panelEntity.ImagePerspectiveBottomRightY
                    });
                }

                if (wall.Panels.Count > 0 &&
                    wall.Panels.All(panel => string.IsNullOrWhiteSpace(panel.ImagePath)) &&
                    !string.IsNullOrWhiteSpace(wall.ImagePath))
                {
                    var firstPanel = wall.Panels[0];
                    firstPanel.ImagePath = wall.ImagePath;
                    firstPanel.ImageOffsetX = wall.ImageOffsetX;
                    firstPanel.ImageOffsetY = wall.ImageOffsetY;
                    firstPanel.ImageScale = wall.ImageScale <= 0 ? 1d : wall.ImageScale;
                    firstPanel.ImageOpacity = wall.ImageOpacity <= 0 ? 0.55d : wall.ImageOpacity;
                }

                var wallHoleEntities = holes
                    .Where(hole => hole.WallId == wallEntity.Id)
                    .GroupBy(BuildHoleEntityKey)
                    .Select(group => group.OrderByDescending(hole => hole.Id).First())
                    .ToList();

                if (wallHoleEntities.Count == 0)
                {
                    wall.RegenerateHoleLayoutFromPanels();
                    foreach (var generatedHole in wall.HoleLayout)
                    {
                        await connection.InsertAsync(new WallHoleEntity
                        {
                            WallId = wallEntity.Id,
                            PanelName = generatedHole.PanelName,
                            PanelX = generatedHole.PanelX,
                            PanelY = generatedHole.PanelY,
                            RelativeX = generatedHole.RelativeX,
                            RelativeY = generatedHole.RelativeY,
                            AbsoluteX = generatedHole.AbsoluteX,
                            AbsoluteY = generatedHole.AbsoluteY,
                            PointId = generatedHole.PointId,
                            LedIndex = generatedHole.LedIndex,
                            IsEnabled = generatedHole.IsEnabled,
                            HasHold = generatedHole.HasHold,
                            HoldSize = (int)generatedHole.HoldSize,
                            HoldType = (int)generatedHole.HoldType,
                            HasEstimatedHoldMetadata = generatedHole.HasEstimatedHoldMetadata,
                            SourceKind = (int)generatedHole.SourceKind
                        });
                    }
                }
                else
                {
                    foreach (var holeEntity in wallHoleEntities)
                    {
                        wall.HoleLayout.Add(new WallHoleDefinition(
                            0,
                            holeEntity.PanelName,
                            holeEntity.PanelX,
                            holeEntity.PanelY,
                            holeEntity.RelativeX,
                            holeEntity.RelativeY,
                            holeEntity.AbsoluteX,
                            holeEntity.AbsoluteY,
                            holeEntity.PointId,
                            holeEntity.LedIndex,
                            holeEntity.IsEnabled,
                            holeEntity.HasHold,
                            (HoldSize)holeEntity.HoldSize,
                            (HoldType)holeEntity.HoldType,
                            holeEntity.HasEstimatedHoldMetadata,
                            Enum.IsDefined(typeof(WallHoleSourceKind), holeEntity.SourceKind)
                                ? (WallHoleSourceKind)holeEntity.SourceKind
                                : WallHoleSourceKind.Generated));
                    }
                }

                wall.AutoAssignLedIndicesByWallRouting();
                result.Add(wall);
            }

            return result;
        });
    }

    private static string BuildHoleEntityKey(WallHoleEntity hole)
    {
        return FormattableString.Invariant(
            $"{hole.PanelName}|{hole.RelativeX:0.####}|{hole.RelativeY:0.####}");
    }
}
