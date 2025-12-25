using Microsoft.Data.Sqlite;
using LeHub.Models;
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;

namespace LeHub.Services;

public class DatabaseService
{
    private readonly string _connectionString;
    private readonly string _dbPath;
    private static DatabaseService? _instance;
    public static DatabaseService Instance => _instance ??= new DatabaseService();

    private DatabaseService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LeHub");

        Directory.CreateDirectory(appDataPath);

        _dbPath = Path.Combine(appDataPath, "lehub.db");
        _connectionString = $"Data Source={_dbPath}";

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Create base tables
        var createTablesCmd = connection.CreateCommand();
        createTablesCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS categories (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                color TEXT,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS apps (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                exe_path TEXT NOT NULL,
                args TEXT,
                is_favorite INTEGER DEFAULT 0,
                category_id INTEGER,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP,
                updated_at TEXT DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (category_id) REFERENCES categories(id) ON DELETE SET NULL
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

            CREATE TABLE IF NOT EXISTS projects (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                root_path TEXT NOT NULL,
                project_type TEXT NOT NULL,
                framework TEXT,
                last_publish_mode TEXT,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP,
                updated_at TEXT DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS project_tags (
                project_id INTEGER NOT NULL,
                tag_id INTEGER NOT NULL,
                PRIMARY KEY (project_id, tag_id),
                FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE CASCADE,
                FOREIGN KEY (tag_id) REFERENCES tags(id) ON DELETE CASCADE
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

        // Check and add category_id column (new FK-based system)
        if (!ColumnExists(connection, "apps", "category_id"))
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE apps ADD COLUMN category_id INTEGER REFERENCES categories(id) ON DELETE SET NULL";
            cmd.ExecuteNonQuery();
        }

        // Migrate old string-based category to new FK system
        if (ColumnExists(connection, "apps", "category"))
        {
            MigrateCategoriesFromString(connection);
        }

        // Add source_project_id column for project integration
        if (!ColumnExists(connection, "apps", "source_project_id"))
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE apps ADD COLUMN source_project_id INTEGER";
            cmd.ExecuteNonQuery();
        }

        // Add generated_by_lehub column
        if (!ColumnExists(connection, "apps", "generated_by_lehub"))
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE apps ADD COLUMN generated_by_lehub INTEGER NOT NULL DEFAULT 0";
            cmd.ExecuteNonQuery();
        }
    }

    private void MigrateCategoriesFromString(SqliteConnection connection)
    {
        // Get all distinct non-empty categories from the old string column
        var getCategoriesCmd = connection.CreateCommand();
        getCategoriesCmd.CommandText = "SELECT DISTINCT category FROM apps WHERE category IS NOT NULL AND category != ''";

        var categoriesToMigrate = new List<string>();
        using (var reader = getCategoriesCmd.ExecuteReader())
        {
            while (reader.Read())
            {
                categoriesToMigrate.Add(reader.GetString(0));
            }
        }

        // Create categories and update apps
        foreach (var categoryName in categoriesToMigrate)
        {
            // Create category if not exists
            var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = "INSERT OR IGNORE INTO categories (name) VALUES (@name)";
            insertCmd.Parameters.AddWithValue("@name", categoryName);
            insertCmd.ExecuteNonQuery();

            // Get the category ID
            var getIdCmd = connection.CreateCommand();
            getIdCmd.CommandText = "SELECT id FROM categories WHERE name = @name";
            getIdCmd.Parameters.AddWithValue("@name", categoryName);
            var categoryId = Convert.ToInt32(getIdCmd.ExecuteScalar());

            // Update apps with this category string to use the FK
            var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = "UPDATE apps SET category_id = @catId WHERE category = @catName AND category_id IS NULL";
            updateCmd.Parameters.AddWithValue("@catId", categoryId);
            updateCmd.Parameters.AddWithValue("@catName", categoryName);
            updateCmd.ExecuteNonQuery();
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

        // Create Music category first
        var catCmd = connection.CreateCommand();
        catCmd.CommandText = "INSERT OR IGNORE INTO categories (name) VALUES ('Music')";
        catCmd.ExecuteNonQuery();

        var catIdCmd = connection.CreateCommand();
        catIdCmd.CommandText = "SELECT id FROM categories WHERE name = 'Music'";
        var categoryId = Convert.ToInt32(catIdCmd.ExecuteScalar());

        var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = @"
            INSERT INTO apps (name, exe_path, args, is_favorite, category_id, sort_order)
            VALUES (@name, @path, @args, @fav, @catId, @sort)";
        insertCmd.Parameters.AddWithValue("@name", "Spotify");
        insertCmd.Parameters.AddWithValue("@path", spotifyPath);
        insertCmd.Parameters.AddWithValue("@args", "");
        insertCmd.Parameters.AddWithValue("@fav", 1);
        insertCmd.Parameters.AddWithValue("@catId", categoryId);
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
            SELECT a.id, a.name, a.exe_path, a.args, a.is_favorite, a.category_id, a.sort_order,
                   a.created_at, a.updated_at, c.id as cat_id, c.name as cat_name, c.color as cat_color,
                   a.source_project_id, a.generated_by_lehub
            FROM apps a
            LEFT JOIN categories c ON a.category_id = c.id
            ORDER BY a.sort_order, a.name";

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
                CategoryId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                SortOrder = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                CreatedAt = DateTime.Parse(reader.GetString(7)),
                UpdatedAt = DateTime.Parse(reader.GetString(8)),
                SourceProjectId = reader.IsDBNull(12) ? null : reader.GetInt32(12),
                GeneratedByLeHub = !reader.IsDBNull(13) && reader.GetInt32(13) == 1
            };

            // Load category if exists
            if (!reader.IsDBNull(9))
            {
                app.Category = new Category
                {
                    Id = reader.GetInt32(9),
                    Name = reader.GetString(10),
                    Color = reader.IsDBNull(11) ? null : reader.GetString(11)
                };
            }

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

    public List<Category> GetAllCategories()
    {
        var categories = new List<Category>();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, color, created_at FROM categories ORDER BY name";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            categories.Add(new Category
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Color = reader.IsDBNull(2) ? null : reader.GetString(2),
                CreatedAt = DateTime.Parse(reader.GetString(3))
            });
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
            INSERT INTO apps (name, exe_path, args, is_favorite, category_id, sort_order, source_project_id, generated_by_lehub)
            VALUES (@name, @path, @args, @fav, @catId, @sort, @srcProjId, @genByLeHub);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@name", app.Name);
        cmd.Parameters.AddWithValue("@path", app.ExePath);
        cmd.Parameters.AddWithValue("@args", app.Arguments ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@fav", app.IsFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("@catId", app.CategoryId.HasValue ? app.CategoryId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@sort", nextOrder);
        cmd.Parameters.AddWithValue("@srcProjId", app.SourceProjectId.HasValue ? app.SourceProjectId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@genByLeHub", app.GeneratedByLeHub ? 1 : 0);

        var appId = Convert.ToInt32(cmd.ExecuteScalar());

        foreach (var tag in app.Tags)
        {
            var tagId = GetOrCreateTag(connection, tag.Name);
            LinkAppTag(connection, appId, tagId);
        }

        return appId;
    }

    public int UpdateApp(AppEntry app)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE apps
            SET name = @name, exe_path = @path, args = @args, is_favorite = @fav,
                category_id = @catId, sort_order = @sort, source_project_id = @srcProjId,
                generated_by_lehub = @genByLeHub, updated_at = CURRENT_TIMESTAMP
            WHERE id = @id";
        cmd.Parameters.AddWithValue("@name", app.Name);
        cmd.Parameters.AddWithValue("@path", app.ExePath);
        cmd.Parameters.AddWithValue("@args", app.Arguments ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@fav", app.IsFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("@catId", app.CategoryId.HasValue ? app.CategoryId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@sort", app.SortOrder);
        cmd.Parameters.AddWithValue("@srcProjId", app.SourceProjectId.HasValue ? app.SourceProjectId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@genByLeHub", app.GeneratedByLeHub ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", app.Id);
        var rowsAffected = cmd.ExecuteNonQuery();

        System.Diagnostics.Debug.WriteLine($"[LeHub] DatabaseService.UpdateApp: DbPath='{_dbPath}', Id={app.Id}, Name='{app.Name}', RowsAffected={rowsAffected}");

        var clearCmd = connection.CreateCommand();
        clearCmd.CommandText = "DELETE FROM app_tags WHERE app_id = @id";
        clearCmd.Parameters.AddWithValue("@id", app.Id);
        clearCmd.ExecuteNonQuery();

        foreach (var tag in app.Tags)
        {
            var tagId = GetOrCreateTag(connection, tag.Name);
            LinkAppTag(connection, app.Id, tagId);
        }

        return rowsAffected;
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
            SELECT pa.app_id, pa.order_index, a.id, a.name, a.exe_path, a.args, a.is_favorite,
                   a.category_id, a.sort_order, c.id as cat_id, c.name as cat_name, c.color as cat_color
            FROM preset_apps pa
            INNER JOIN apps a ON pa.app_id = a.id
            LEFT JOIN categories c ON a.category_id = c.id
            WHERE pa.preset_id = @presetId
            ORDER BY pa.order_index";
        cmd.Parameters.AddWithValue("@presetId", presetId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var app = new AppEntry
            {
                Id = reader.GetInt32(2),
                Name = reader.GetString(3),
                ExePath = reader.GetString(4),
                Arguments = reader.IsDBNull(5) ? null : reader.GetString(5),
                IsFavorite = reader.GetInt32(6) == 1,
                CategoryId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                SortOrder = reader.IsDBNull(8) ? 0 : reader.GetInt32(8)
            };

            // Load category if exists
            if (!reader.IsDBNull(9))
            {
                app.Category = new Category
                {
                    Id = reader.GetInt32(9),
                    Name = reader.GetString(10),
                    Color = reader.IsDBNull(11) ? null : reader.GetString(11)
                };
            }

            apps.Add(new PresetApp
            {
                PresetId = presetId,
                AppId = reader.GetInt32(0),
                OrderIndex = reader.GetInt32(1),
                App = app
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

    /// <summary>
    /// Gets a single preset by ID with all its apps loaded.
    /// </summary>
    public Preset? GetPresetById(int presetId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, delay_ms, created_at FROM presets WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", presetId);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var preset = new Preset
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                DelayMs = reader.GetInt32(2),
                CreatedAt = DateTime.Parse(reader.GetString(3))
            };
            reader.Close();
            preset.Apps = GetPresetApps(connection, preset.Id);
            return preset;
        }

        return null;
    }

    /// <summary>
    /// Duplicates a preset with a new name.
    /// </summary>
    public int DuplicatePreset(int presetId, string newName)
    {
        var original = GetPresetById(presetId);
        if (original == null) return -1;

        var duplicate = new Preset
        {
            Name = newName,
            DelayMs = original.DelayMs,
            Apps = original.Apps.Select(a => new PresetApp
            {
                AppId = a.AppId,
                OrderIndex = a.OrderIndex
            }).ToList()
        };

        return AddPreset(duplicate);
    }

    // Category CRUD methods
    public int AddCategory(string name, string? color = null)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO categories (name, color) VALUES (@name, @color); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@color", color ?? (object)DBNull.Value);

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public int GetOrCreateCategory(string name)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var findCmd = connection.CreateCommand();
        findCmd.CommandText = "SELECT id FROM categories WHERE name = @name";
        findCmd.Parameters.AddWithValue("@name", name);
        var result = findCmd.ExecuteScalar();

        if (result != null)
            return Convert.ToInt32(result);

        var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = "INSERT INTO categories (name) VALUES (@name); SELECT last_insert_rowid();";
        insertCmd.Parameters.AddWithValue("@name", name);
        return Convert.ToInt32(insertCmd.ExecuteScalar());
    }

    public void DeleteCategory(int categoryId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Apps will have category_id set to NULL due to ON DELETE SET NULL
        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM categories WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", categoryId);
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

    // Tag CRUD methods (public)
    public int AddTag(string name)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO tags (name) VALUES (@name); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@name", name);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void UpdateTag(int tagId, string newName)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE tags SET name = @name WHERE id = @id";
        cmd.Parameters.AddWithValue("@name", newName);
        cmd.Parameters.AddWithValue("@id", tagId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteTag(int tagId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // First remove all app_tags links
        var linkCmd = connection.CreateCommand();
        linkCmd.CommandText = "DELETE FROM app_tags WHERE tag_id = @id";
        linkCmd.Parameters.AddWithValue("@id", tagId);
        linkCmd.ExecuteNonQuery();

        // Then delete the tag
        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM tags WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", tagId);
        cmd.ExecuteNonQuery();
    }

    // ===== PROJECT CRUD METHODS =====

    public List<Project> GetAllProjects()
    {
        var projects = new List<Project>();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT id, name, root_path, project_type, framework, last_publish_mode, created_at, updated_at
            FROM projects
            ORDER BY name";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var project = new Project
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                RootPath = reader.GetString(2),
                ProjectType = Enum.Parse<ProjectType>(reader.GetString(3)),
                Framework = reader.IsDBNull(4) ? null : reader.GetString(4),
                LastPublishMode = reader.IsDBNull(5) ? null : reader.GetString(5),
                CreatedAt = DateTime.Parse(reader.GetString(6)),
                UpdatedAt = DateTime.Parse(reader.GetString(7))
            };
            project.Tags = GetTagsForProject(connection, project.Id);
            projects.Add(project);
        }

        return projects;
    }

    private List<Tag> GetTagsForProject(SqliteConnection connection, int projectId)
    {
        var tags = new List<Tag>();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT t.id, t.name
            FROM tags t
            INNER JOIN project_tags pt ON t.id = pt.tag_id
            WHERE pt.project_id = @projectId";
        cmd.Parameters.AddWithValue("@projectId", projectId);

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

    public int AddProject(Project project)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO projects (name, root_path, project_type, framework, last_publish_mode)
            VALUES (@name, @rootPath, @type, @framework, @lastMode);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@name", project.Name);
        cmd.Parameters.AddWithValue("@rootPath", project.RootPath);
        cmd.Parameters.AddWithValue("@type", project.ProjectType.ToString());
        cmd.Parameters.AddWithValue("@framework", project.Framework ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@lastMode", project.LastPublishMode ?? (object)DBNull.Value);

        var projectId = Convert.ToInt32(cmd.ExecuteScalar());

        foreach (var tag in project.Tags)
        {
            var tagId = GetOrCreateTag(connection, tag.Name);
            LinkProjectTag(connection, projectId, tagId);
        }

        return projectId;
    }

    public void UpdateProject(Project project)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE projects
            SET name = @name, root_path = @rootPath, project_type = @type, framework = @framework,
                last_publish_mode = @lastMode, updated_at = CURRENT_TIMESTAMP
            WHERE id = @id";
        cmd.Parameters.AddWithValue("@name", project.Name);
        cmd.Parameters.AddWithValue("@rootPath", project.RootPath);
        cmd.Parameters.AddWithValue("@type", project.ProjectType.ToString());
        cmd.Parameters.AddWithValue("@framework", project.Framework ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@lastMode", project.LastPublishMode ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@id", project.Id);
        cmd.ExecuteNonQuery();

        // Clear and re-add tags
        var clearCmd = connection.CreateCommand();
        clearCmd.CommandText = "DELETE FROM project_tags WHERE project_id = @id";
        clearCmd.Parameters.AddWithValue("@id", project.Id);
        clearCmd.ExecuteNonQuery();

        foreach (var tag in project.Tags)
        {
            var tagId = GetOrCreateTag(connection, tag.Name);
            LinkProjectTag(connection, project.Id, tagId);
        }
    }

    public void UpdateProjectPublishMode(int projectId, string mode)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE projects SET last_publish_mode = @mode, updated_at = CURRENT_TIMESTAMP WHERE id = @id";
        cmd.Parameters.AddWithValue("@mode", mode);
        cmd.Parameters.AddWithValue("@id", projectId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteProject(int projectId, bool deleteLinkedApp)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        if (deleteLinkedApp)
        {
            // Find and delete the linked app
            var findAppCmd = connection.CreateCommand();
            findAppCmd.CommandText = "SELECT id FROM apps WHERE source_project_id = @projectId";
            findAppCmd.Parameters.AddWithValue("@projectId", projectId);
            var appIdResult = findAppCmd.ExecuteScalar();

            if (appIdResult != null)
            {
                var appId = Convert.ToInt32(appIdResult);

                // Delete app_tags
                var delTagsCmd = connection.CreateCommand();
                delTagsCmd.CommandText = "DELETE FROM app_tags WHERE app_id = @id";
                delTagsCmd.Parameters.AddWithValue("@id", appId);
                delTagsCmd.ExecuteNonQuery();

                // Delete preset_apps
                var delPresetCmd = connection.CreateCommand();
                delPresetCmd.CommandText = "DELETE FROM preset_apps WHERE app_id = @id";
                delPresetCmd.Parameters.AddWithValue("@id", appId);
                delPresetCmd.ExecuteNonQuery();

                // Delete app
                var delAppCmd = connection.CreateCommand();
                delAppCmd.CommandText = "DELETE FROM apps WHERE id = @id";
                delAppCmd.Parameters.AddWithValue("@id", appId);
                delAppCmd.ExecuteNonQuery();
            }
        }
        else
        {
            // Just unlink the app (set source_project_id to NULL)
            var unlinkCmd = connection.CreateCommand();
            unlinkCmd.CommandText = "UPDATE apps SET source_project_id = NULL WHERE source_project_id = @projectId";
            unlinkCmd.Parameters.AddWithValue("@projectId", projectId);
            unlinkCmd.ExecuteNonQuery();
        }

        // Delete project_tags
        var linkCmd = connection.CreateCommand();
        linkCmd.CommandText = "DELETE FROM project_tags WHERE project_id = @id";
        linkCmd.Parameters.AddWithValue("@id", projectId);
        linkCmd.ExecuteNonQuery();

        // Delete project
        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM projects WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", projectId);
        cmd.ExecuteNonQuery();
    }

    private void LinkProjectTag(SqliteConnection connection, int projectId, int tagId)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO project_tags (project_id, tag_id) VALUES (@projectId, @tagId)";
        cmd.Parameters.AddWithValue("@projectId", projectId);
        cmd.Parameters.AddWithValue("@tagId", tagId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Gets the app linked to a project (for upsert logic).
    /// Returns null if no app is linked.
    /// </summary>
    public AppEntry? GetAppBySourceProject(int projectId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT a.id, a.name, a.exe_path, a.args, a.is_favorite, a.category_id, a.sort_order,
                   a.created_at, a.updated_at, a.source_project_id, a.generated_by_lehub
            FROM apps a
            WHERE a.source_project_id = @projectId
            LIMIT 1";
        cmd.Parameters.AddWithValue("@projectId", projectId);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var app = new AppEntry
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                ExePath = reader.GetString(2),
                Arguments = reader.IsDBNull(3) ? null : reader.GetString(3),
                IsFavorite = reader.GetInt32(4) == 1,
                CategoryId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                SortOrder = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                CreatedAt = DateTime.Parse(reader.GetString(7)),
                UpdatedAt = DateTime.Parse(reader.GetString(8)),
                SourceProjectId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                GeneratedByLeHub = !reader.IsDBNull(10) && reader.GetInt32(10) == 1
            };
            app.Tags = GetTagsForApp(connection, app.Id);
            return app;
        }

        return null;
    }

    /// <summary>
    /// Checks if a project has a linked app.
    /// </summary>
    public bool ProjectHasLinkedApp(int projectId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM apps WHERE source_project_id = @projectId";
        cmd.Parameters.AddWithValue("@projectId", projectId);

        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }
}
