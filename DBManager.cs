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

        public static void OpenDBConnection()
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
            if(connection.State != System.Data.ConnectionState.Closed)
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

        public static List<Image> GetImages(List<ImageSource> sources, Color c, int error)
        {
            if(sources.Count == 0)
                return new List<Image>();
            String sourcePathCondition = "(Sources.path = '" + sources[0].path + "'";
            for(int i = 1; i < sources.Count; i++)
            {
                sourcePathCondition += " OR Sources.path = '" + sources[i].path + "'";
            }
            sourcePathCondition += ") ";
            String sql = "SELECT Images.path, red, green, blue FROM Images " +
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
                        imageList.Add(new Image((String)reader["path"], color));
                    }
                    return imageList;
                }
            }
        }

        public static void AddImage(ImageSource source, String path, Color c, String hashcode)
        {
            int sourceId = GetSourceId(source);
            String sql = "INSERT INTO Images (sourceId, path, red, green, blue, hashcode) " +
                         "VALUES (@sourceId, @path, @red, @green, @blue, @hashcode)";
            using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
            {
                cmd.Parameters.Add(new SQLiteParameter("@sourceId", sourceId));
                cmd.Parameters.Add(new SQLiteParameter("@path", path));
                cmd.Parameters.Add(new SQLiteParameter("@red", c.R));
                cmd.Parameters.Add(new SQLiteParameter("@green", c.G));
                cmd.Parameters.Add(new SQLiteParameter("@blue", c.B));
                cmd.Parameters.Add(new SQLiteParameter("@hashcode", hashcode));
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

        public static ImageSource.Type GetImageSourceType(String imagePath)
        {
            String sql = "SELECT type FROM Sources " +
                         "INNER JOIN Images ON Images.path = @path AND Images.sourceId = Sources.id";
            using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
            {
                cmd.Parameters.Add(new SQLiteParameter("@path", imagePath));
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
