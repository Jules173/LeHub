using Microsoft.Data.Sqlite;
using LeHub.Models;
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;

namespace LeHub.Services;

public class DatabaseService
{
    private readonly string _connectionString;
    private static DatabaseService? _instance;
    public static DatabaseService Instance => _instance ??= new DatabaseService();

    private DatabaseService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LeHub");

        Directory.CreateDirectory(appDataPath);

        var dbPath = Path.Combine(appDataPath, "lehub.db");
        _connectionString = $"Data Source={dbPath}";

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Create base tables
        var createTablesCmd = connection.CreateCommand();
        createTablesCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS apps (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                exe_path TEXT NOT NULL,
                args TEXT,
                is_favorite INTEGER DEFAULT 0,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP,
                updated_at TEXT DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS tags (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS app_tags (
                app_id INTEGER NOT NULL,
                tag_id INTEGER NOT NULL,
                PRIMARY KEY (app_id, tag_id),
                FOREIGN KEY (app_id) REFERENCES apps(id) ON DELETE CASCADE,
                FOREIGN KEY (tag_id) REFERENCES tags(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS presets (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                delay_ms INTEGER DEFAULT 200,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS preset_apps (
                preset_id INTEGER NOT NULL,
                app_id INTEGER NOT NULL,
                order_index INTEGER DEFAULT 0,
                PRIMARY KEY (preset_id, app_id),
                FOREIGN KEY (preset_id) REFERENCES presets(id) ON DELETE CASCADE,
                FOREIGN KEY (app_id) REFERENCES apps(id) ON DELETE CASCADE
            );
        ";
        createTablesCmd.ExecuteNonQuery();

        // Run migrations for new columns
        RunMigrations(connection);

        // Seed Spotify if DB is empty
        var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM apps";
        var count = Convert.ToInt64(countCmd.ExecuteScalar());

        if (count == 0)
        {
            SeedSpotify(connection);
        }
    }

    private void RunMigrations(SqliteConnection connection)
    {
        // Check and add sort_order column
        if (!ColumnExists(connection, "apps", "sort_order"))
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE apps ADD COLUMN sort_order INTEGER DEFAULT 0";
            cmd.ExecuteNonQuery();
        }

        // Check and add category column
        if (!ColumnExists(connection, "apps", "category"))
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE apps ADD COLUMN category TEXT DEFAULT ''";
            cmd.ExecuteNonQuery();
        }
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private void SeedSpotify(SqliteConnection connection)
    {
        var spotifyPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spotify", "Spotify.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "Spotify.exe"),
            @"C:\Program Files\Spotify\Spotify.exe",
            @"C:\Program Files (x86)\Spotify\Spotify.exe"
        };

        var spotifyPath = spotifyPaths.FirstOrDefault(File.Exists) ?? "";

        var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = @"
            INSERT INTO apps (name, exe_path, args, is_favorite, category, sort_order)
            VALUES (@name, @path, @args, @fav, @cat, @sort)";
        insertCmd.Parameters.AddWithValue("@name", "Spotify");
        insertCmd.Parameters.AddWithValue("@path", spotifyPath);
        insertCmd.Parameters.AddWithValue("@args", "");
        insertCmd.Parameters.AddWithValue("@fav", 1);
        insertCmd.Parameters.AddWithValue("@cat", "Music");
        insertCmd.Parameters.AddWithValue("@sort", 0);
        insertCmd.ExecuteNonQuery();

        var tagCmd = connection.CreateCommand();
        tagCmd.CommandText = "INSERT INTO tags (name) VALUES ('Music')";
        tagCmd.ExecuteNonQuery();

        var tagIdCmd = connection.CreateCommand();
        tagIdCmd.CommandText = "SELECT last_insert_rowid()";
        var tagId = Convert.ToInt64(tagIdCmd.ExecuteScalar());

        var linkCmd = connection.CreateCommand();
        linkCmd.CommandText = @"
            INSERT INTO app_tags (app_id, tag_id)
            SELECT id, @tagId FROM apps WHERE name = 'Spotify'";
        linkCmd.Parameters.AddWithValue("@tagId", tagId);
        linkCmd.ExecuteNonQuery();
    }

    public List<AppEntry> GetAllApps()
    {
        var apps = new List<AppEntry>();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT id, name, exe_path, args, is_favorite, category, sort_order, created_at, updated_at
            FROM apps
            ORDER BY sort_order, name";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var app = new AppEntry
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                ExePath = reader.GetString(2),
                Arguments = reader.IsDBNull(3) ? null : reader.GetString(3),
                IsFavorite = reader.GetInt32(4) == 1,
                Category = reader.IsDBNull(5) ? "" : reader.GetString(5),
                SortOrder = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                CreatedAt = DateTime.Parse(reader.GetString(7)),
                UpdatedAt = DateTime.Parse(reader.GetString(8))
            };

            app.Tags = GetTagsForApp(connection, app.Id);
            apps.Add(app);
        }

        return apps;
    }

    private List<Tag> GetTagsForApp(SqliteConnection connection, int appId)
    {
        var tags = new List<Tag>();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT t.id, t.name
            FROM tags t
            INNER JOIN app_tags at ON t.id = at.tag_id
            WHERE at.app_id = @appId";
        cmd.Parameters.AddWithValue("@appId", appId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tags.Add(new Tag
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }

        return tags;
    }

    public List<Tag> GetAllTags()
    {
        var tags = new List<Tag>();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, name FROM tags ORDER BY name";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tags.Add(new Tag
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }

        return tags;
    }

    public List<string> GetAllCategories()
    {
        var categories = new List<string>();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT category FROM apps WHERE category != '' ORDER BY category";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            categories.Add(reader.GetString(0));
        }

        return categories;
    }

    public int AddApp(AppEntry app)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Get max sort order
        var maxOrderCmd = connection.CreateCommand();
        maxOrderCmd.CommandText = "SELECT COALESCE(MAX(sort_order), -1) + 1 FROM apps";
        var nextOrder = Convert.ToInt32(maxOrderCmd.ExecuteScalar());

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO apps (name, exe_path, args, is_favorite, category, sort_order)
            VALUES (@name, @path, @args, @fav, @cat, @sort);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@name", app.Name);
        cmd.Parameters.AddWithValue("@path", app.ExePath);
        cmd.Parameters.AddWithValue("@args", app.Arguments ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@fav", app.IsFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("@cat", app.Category ?? "");
        cmd.Parameters.AddWithValue("@sort", nextOrder);

        var appId = Convert.ToInt32(cmd.ExecuteScalar());

        foreach (var tag in app.Tags)
        {
            var tagId = GetOrCreateTag(connection, tag.Name);
            LinkAppTag(connection, appId, tagId);
        }

        return appId;
    }

    public void UpdateApp(AppEntry app)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE apps
            SET name = @name, exe_path = @path, args = @args, is_favorite = @fav,
                category = @cat, sort_order = @sort, updated_at = CURRENT_TIMESTAMP
            WHERE id = @id";
        cmd.Parameters.AddWithValue("@name", app.Name);
        cmd.Parameters.AddWithValue("@path", app.ExePath);
        cmd.Parameters.AddWithValue("@args", app.Arguments ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@fav", app.IsFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("@cat", app.Category ?? "");
        cmd.Parameters.AddWithValue("@sort", app.SortOrder);
        cmd.Parameters.AddWithValue("@id", app.Id);
        cmd.ExecuteNonQuery();

        var clearCmd = connection.CreateCommand();
        clearCmd.CommandText = "DELETE FROM app_tags WHERE app_id = @id";
        clearCmd.Parameters.AddWithValue("@id", app.Id);
        clearCmd.ExecuteNonQuery();

        foreach (var tag in app.Tags)
        {
            var tagId = GetOrCreateTag(connection, tag.Name);
            LinkAppTag(connection, app.Id, tagId);
        }
    }

    public void UpdateSortOrder(int appId, int sortOrder)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE apps SET sort_order = @sort, updated_at = CURRENT_TIMESTAMP WHERE id = @id";
        cmd.Parameters.AddWithValue("@sort", sortOrder);
        cmd.Parameters.AddWithValue("@id", appId);
        cmd.ExecuteNonQuery();
    }

    public void UpdateFavorite(int appId, bool isFavorite)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE apps SET is_favorite = @fav, updated_at = CURRENT_TIMESTAMP WHERE id = @id";
        cmd.Parameters.AddWithValue("@fav", isFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", appId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteApp(int appId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var linkCmd = connection.CreateCommand();
        linkCmd.CommandText = "DELETE FROM app_tags WHERE app_id = @id";
        linkCmd.Parameters.AddWithValue("@id", appId);
        linkCmd.ExecuteNonQuery();

        var presetLinkCmd = connection.CreateCommand();
        presetLinkCmd.CommandText = "DELETE FROM preset_apps WHERE app_id = @id";
        presetLinkCmd.Parameters.AddWithValue("@id", appId);
        presetLinkCmd.ExecuteNonQuery();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM apps WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", appId);
        cmd.ExecuteNonQuery();
    }

    // Preset methods
    public List<Preset> GetAllPresets()
    {
        var presets = new List<Preset>();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, delay_ms, created_at FROM presets ORDER BY name";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var preset = new Preset
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                DelayMs = reader.GetInt32(2),
                CreatedAt = DateTime.Parse(reader.GetString(3))
            };
            presets.Add(preset);
        }

        // Load apps for each preset
        foreach (var preset in presets)
        {
            preset.Apps = GetPresetApps(connection, preset.Id);
        }

        return presets;
    }

    private List<PresetApp> GetPresetApps(SqliteConnection connection, int presetId)
    {
        var apps = new List<PresetApp>();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT pa.app_id, pa.order_index, a.id, a.name, a.exe_path, a.args, a.is_favorite, a.category, a.sort_order
            FROM preset_apps pa
            INNER JOIN apps a ON pa.app_id = a.id
            WHERE pa.preset_id = @presetId
            ORDER BY pa.order_index";
        cmd.Parameters.AddWithValue("@presetId", presetId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            apps.Add(new PresetApp
            {
                PresetId = presetId,
                AppId = reader.GetInt32(0),
                OrderIndex = reader.GetInt32(1),
                App = new AppEntry
                {
                    Id = reader.GetInt32(2),
                    Name = reader.GetString(3),
                    ExePath = reader.GetString(4),
                    Arguments = reader.IsDBNull(5) ? null : reader.GetString(5),
                    IsFavorite = reader.GetInt32(6) == 1,
                    Category = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    SortOrder = reader.IsDBNull(8) ? 0 : reader.GetInt32(8)
                }
            });
        }

        return apps;
    }

    public int AddPreset(Preset preset)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO presets (name, delay_ms) VALUES (@name, @delay);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@name", preset.Name);
        cmd.Parameters.AddWithValue("@delay", preset.DelayMs);

        var presetId = Convert.ToInt32(cmd.ExecuteScalar());

        for (var i = 0; i < preset.Apps.Count; i++)
        {
            var appCmd = connection.CreateCommand();
            appCmd.CommandText = "INSERT INTO preset_apps (preset_id, app_id, order_index) VALUES (@pid, @aid, @idx)";
            appCmd.Parameters.AddWithValue("@pid", presetId);
            appCmd.Parameters.AddWithValue("@aid", preset.Apps[i].AppId);
            appCmd.Parameters.AddWithValue("@idx", i);
            appCmd.ExecuteNonQuery();
        }

        return presetId;
    }

    public void UpdatePreset(Preset preset)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE presets SET name = @name, delay_ms = @delay WHERE id = @id";
        cmd.Parameters.AddWithValue("@name", preset.Name);
        cmd.Parameters.AddWithValue("@delay", preset.DelayMs);
        cmd.Parameters.AddWithValue("@id", preset.Id);
        cmd.ExecuteNonQuery();

        // Clear and re-add apps
        var clearCmd = connection.CreateCommand();
        clearCmd.CommandText = "DELETE FROM preset_apps WHERE preset_id = @id";
        clearCmd.Parameters.AddWithValue("@id", preset.Id);
        clearCmd.ExecuteNonQuery();

        for (var i = 0; i < preset.Apps.Count; i++)
        {
            var appCmd = connection.CreateCommand();
            appCmd.CommandText = "INSERT INTO preset_apps (preset_id, app_id, order_index) VALUES (@pid, @aid, @idx)";
            appCmd.Parameters.AddWithValue("@pid", preset.Id);
            appCmd.Parameters.AddWithValue("@aid", preset.Apps[i].AppId);
            appCmd.Parameters.AddWithValue("@idx", i);
            appCmd.ExecuteNonQuery();
        }
    }

    public void DeletePreset(int presetId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var linkCmd = connection.CreateCommand();
        linkCmd.CommandText = "DELETE FROM preset_apps WHERE preset_id = @id";
        linkCmd.Parameters.AddWithValue("@id", presetId);
        linkCmd.ExecuteNonQuery();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM presets WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", presetId);
        cmd.ExecuteNonQuery();
    }

    private int GetOrCreateTag(SqliteConnection connection, string tagName)
    {
        var findCmd = connection.CreateCommand();
        findCmd.CommandText = "SELECT id FROM tags WHERE name = @name";
        findCmd.Parameters.AddWithValue("@name", tagName);
        var result = findCmd.ExecuteScalar();

        if (result != null)
            return Convert.ToInt32(result);

        var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = "INSERT INTO tags (name) VALUES (@name); SELECT last_insert_rowid();";
        insertCmd.Parameters.AddWithValue("@name", tagName);
        return Convert.ToInt32(insertCmd.ExecuteScalar());
    }

    private void LinkAppTag(SqliteConnection connection, int appId, int tagId)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO app_tags (app_id, tag_id) VALUES (@appId, @tagId)";
        cmd.Parameters.AddWithValue("@appId", appId);
        cmd.Parameters.AddWithValue("@tagId", tagId);
        cmd.ExecuteNonQuery();
    }
}
