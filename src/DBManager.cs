using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.IO;

namespace Mosaic
{
    internal static class DBManager
    {
        public const String dbName = "IndexedImageSources.db";
        private static SQLiteConnection connection;

        static DBManager()
        {
            connection = new SQLiteConnection("DataSource=" + dbName + ";Version=3;");
            if (File.Exists(dbName) == false)
            {
                CreateDatabase();
            }
            else
            {
                connection.Open();
            }
        }

        public static void CloseDBConnection()
        {
            if(connection != null && connection.State != System.Data.ConnectionState.Closed)
            {
                connection.Close();
            }
        }

        public static int CountImages(ImageSource source)
        {
            String sql = "SELECT Count(*) FROM Images " +
                         "INNER JOIN Sources ON Sources.id = Images.sourceId AND Sources.path = @path";
            using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
            {
                cmd.Parameters.Add(new SQLiteParameter("@path", source.path));
                int number = Convert.ToInt32(cmd.ExecuteScalar());
                return number;
            }
        }

        public static List<ImageSource> GetUsedSources()
        {
            String sql = "SELECT * FROM Sources Where isUsed = 1";
            using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
            {
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    List<ImageSource> sourceList = new List<ImageSource>();
                    while (reader.Read())
                    {
                        ImageSource source = new ImageSource((String)reader["name"], (String)reader["path"], (ImageSource.Type)(Int64)reader["type"], 0, true);
                        source.imageCount = CountImages(source);
                        sourceList.Add(source);
                    }
                    return sourceList;
                }
            }
        }

        public static List<ImageSource> GetAllSources()
        {
            String sql = "SELECT * FROM Sources";
            using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
            {
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    List<ImageSource> sourceList = new List<ImageSource>();
                    while (reader.Read())
                    {
                        ImageSource source = new ImageSource((String)reader["name"], (String)reader["path"], 
                                                             (ImageSource.Type)(Int64)reader["type"], 0, (bool)reader["isUsed"]);
                        source.imageCount = CountImages(source);
                        sourceList.Add(source);
                    }
                    return sourceList;
                }
            }
        }

        /// <summary>
        /// Returns null if there are no sources or no images.
        /// </summary>
        public static Image GetRandomImage(List<ImageSource> sources)
        {
            if (sources.Count == 0)
                return null;
            String sourcePathCondition = MakeSourcePathConditionLine(sources);
            String sql = "SELECT * FROM Images " +
                         "INNER JOIN Sources ON Sources.id = Images.sourceId AND " + sourcePathCondition +
                         "ORDER BY RANDOM() " +
                         "LIMIT 1";
            Image image = null;
            using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
            {
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Color color = Color.FromArgb((int)(Int64)reader["red"], (int)(Int64)reader["green"], (int)(Int64)reader["blue"]);
                        image = new Image((String)reader["path"], color, (String)reader["hashcode"]);
                    }                    
                }
            }
            return image;
        }

        public static int GetImageCount(List<ImageSource> sources)
        {
            String sourcePathCondition = MakeSourcePathConditionLine(sources);
            String sql = "SELECT COUNT(*) FROM Images " +
                         "INNER JOIN Sources ON Sources.id = Images.sourceId AND " + sourcePathCondition;
            using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
            {
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public static List<Image> GetImages(List<ImageSource> sources, Color c, int error)
        {
            if(sources.Count == 0)
                return new List<Image>();
            String sourcePathCondition = MakeSourcePathConditionLine(sources);
            String sql = "SELECT * FROM Images " +
                         "INNER JOIN Sources ON Sources.id = Images.sourceId AND " + sourcePathCondition +
                         "WHERE (red BETWEEN @redMin AND @redMax) AND " +
                               "(blue BETWEEN @blueMin AND @blueMax) AND " +
                               "(green BETWEEN @greenMin AND @greenMax)";
            using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
            {
                cmd.Parameters.Add(new SQLiteParameter("@redMin", c.R - error));
                cmd.Parameters.Add(new SQLiteParameter("@greenMin", c.G - error));
                cmd.Parameters.Add(new SQLiteParameter("@blueMin", c.B - error));
                cmd.Parameters.Add(new SQLiteParameter("@redMax", c.R + error));
                cmd.Parameters.Add(new SQLiteParameter("@greenMax", c.G + error));
                cmd.Parameters.Add(new SQLiteParameter("@blueMax", c.B + error));
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    List<Image> imageList = new List<Image>();
                    while (reader.Read())
                    {
                        Color color = Color.FromArgb((int)(Int64)reader["red"], (int)(Int64)reader["green"], (int)(Int64)reader["blue"]);
                        imageList.Add(new Image((String)reader["path"], color, (String)reader["hashcode"]));
                    }
                    return imageList;
                }
            }
        }

        /// <summary>
        /// Returns a string with the format: "(Sources.path = sourcepath1 OR ... OR Sources.path = sourcepathN) "
        /// </summary>
        private static String MakeSourcePathConditionLine(List<ImageSource> sources)
        {
            String sourcePathCondition = "(Sources.path = '" + sources[0].path + "'";
            for (int i = 1; i < sources.Count; i++)
            {
                sourcePathCondition += " OR Sources.path = '" + sources[i].path + "'";
            }
            sourcePathCondition += ") ";
            return sourcePathCondition;
        }

        public static void AddImage(ImageSource source, Image image)
        {
            int sourceId = GetSourceId(source);
            String sql = "INSERT INTO Images (sourceId, path, red, green, blue, hashcode) " +
                         "VALUES (@sourceId, @path, @red, @green, @blue, @hashcode)";
            using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
            {
                cmd.Parameters.Add(new SQLiteParameter("@sourceId", sourceId));
                cmd.Parameters.Add(new SQLiteParameter("@path", image.path));
                cmd.Parameters.Add(new SQLiteParameter("@red", image.color.R));
                cmd.Parameters.Add(new SQLiteParameter("@green", image.color.G));
                cmd.Parameters.Add(new SQLiteParameter("@blue", image.color.B));
                cmd.Parameters.Add(new SQLiteParameter("@hashcode", image.hashcode));
                cmd.ExecuteNonQuery();
            }            
        }

        public static void AddSource(ImageSource source)
        {
            String sql = "INSERT INTO Sources (path, name, type, isUsed) VALUES (@path, @name, @type, @isUsed)";
            using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
            {
                cmd.Parameters.Add(new SQLiteParameter("@path", source.path));
                cmd.Parameters.Add(new SQLiteParameter("@name", source.name));
                cmd.Parameters.Add(new SQLiteParameter("@type", (int)source.type));
                cmd.Parameters.Add(new SQLiteParameter("@isUsed", source.isUsed));
                cmd.ExecuteNonQuery();
            }            
        }

        public static bool ContainsSource(ImageSource source)
        {
            String sql = "SELECT * FROM Sources WHERE path = @path";
            using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
            {
                cmd.Parameters.Add(new SQLiteParameter("@path", source.path));
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {             
                    return reader.Read();
                }
            }
        }

        public static ImageSource.Type GetImageSourceType(Image image)
        {
            String sql = "SELECT type FROM Sources " +
                         "INNER JOIN Images ON Images.path = @path AND Images.sourceId = Sources.id";
            using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
            {
                cmd.Parameters.Add(new SQLiteParameter("@path", image.path));
                var type = (ImageSource.Type)(Int64)cmd.ExecuteScalar();             
                return type;
            }
        }

        public static void UpdateIsUsedField(ImageSource source)
        {
            String sql = "UPDATE Sources SET isUsed = @isUsed WHERE path = @path";
            using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
            {
                cmd.Parameters.Add(new SQLiteParameter("@path", source.path));
                cmd.Parameters.Add(new SQLiteParameter("@isUsed", source.isUsed));
                cmd.ExecuteNonQuery();
            }            
        }

        public static void RemoveSource(ImageSource source)
        {
            int sourceId = GetSourceId(source);
            String sql = "DELETE FROM Sources WHERE id = @id";
            using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
            {
                cmd.Parameters.Add(new SQLiteParameter("@id", sourceId));
                cmd.ExecuteNonQuery();             
                RemoveImages(sourceId);
            }
        }

        private static void CreateDatabase()
        {
            SQLiteConnection.CreateFile(dbName);
            connection.Open();
            CreateTables();
        }

        private static void CreateTables()
        {
            String sql = "CREATE TABLE Sources (id INTEGER PRIMARY KEY ASC, path TEXT, name TEXT, type INTEGER, isUsed BOOLEAN);";
            SQLiteCommand cmd = null;
            using (cmd = new SQLiteCommand(sql, connection))
            {
                cmd.ExecuteNonQuery();     
            }      
            sql = "CREATE TABLE Images (sourceId INTEGER, path TEXT, red INTEGER, green INTEGER, blue INTEGER, hashcode TEXT);";
            using(cmd = new SQLiteCommand(sql, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private static int GetSourceId(ImageSource source)
        {
            String sql = "SELECT id FROM Sources WHERE path = @path AND name = @name AND type = @type";
            using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
            {
                cmd.Parameters.Add(new SQLiteParameter("@path", source.path));
                cmd.Parameters.Add(new SQLiteParameter("@name", source.name));
                cmd.Parameters.Add(new SQLiteParameter("@type", (int)source.type));
                int sourceId = Convert.ToInt32(cmd.ExecuteScalar());
                return sourceId;
            }
        }  
      
        /// <summary>
        /// Removes all images from the source with specified Id.
        /// </summary>
        private static void RemoveImages(int sourceId)
        {
            String sql = "DELETE FROM Images WHERE sourceId = @id";
            using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
            {
                cmd.Parameters.Add(new SQLiteParameter("@id", sourceId));
                cmd.ExecuteNonQuery();             
            }
        }
    }
}
