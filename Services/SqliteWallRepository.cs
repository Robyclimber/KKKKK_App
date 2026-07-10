using SQLite;
using WallPanelPlanner.Models;
using WallPanelPlanner.Persistence;
using WallPanelPlanner.Persistence.Entities;

namespace WallPanelPlanner.Services;

public sealed class SqliteWallRepository : IWallRepository
{
    private readonly ISqliteDatabaseFactory databaseFactory;

    public SqliteWallRepository(ISqliteDatabaseFactory databaseFactory)
    {
        this.databaseFactory = databaseFactory;
    }

    public async Task<int> SaveAsync(WallDefinition wall, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wall);

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
                EdgeOffsetY = panel.EdgeOffsetY
            });
        }

        if (wall.HoleLayout.Count == 0)
        {
            wall.RegenerateHoleLayoutFromPanels();
        }

        foreach (var hole in wall.HoleLayout)
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
                HasHold = hole.HasHold,
                HoldSize = (int)hole.HoldSize,
                HoldType = (int)hole.HoldType
            });
        }

        wall.Id = entity.Id;
        return entity.Id;
    }

    public async Task<IReadOnlyList<WallDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
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
                ImageOpacity = wallEntity.ImageOpacity
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
                    EdgeOffsetY = panelEntity.EdgeOffsetY
                });
            }

            var wallHoleEntities = holes
                .Where(hole => hole.WallId == wallEntity.Id)
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
                        HasHold = generatedHole.HasHold,
                        HoldSize = (int)generatedHole.HoldSize,
                        HoldType = (int)generatedHole.HoldType
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
                        holeEntity.HasHold,
                        (HoldSize)holeEntity.HoldSize,
                        (HoldType)holeEntity.HoldType));
                }
            }

            result.Add(wall);
        }

        return result;
    }
}
