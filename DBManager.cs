using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.SQLite;
using System.Drawing;

namespace Mosaic
{
    class DBManager
    {
        public const String dbName = "IndexedImageSources.db";
        private static SQLiteConnection connection = new SQLiteConnection("DataSource=" + dbName + ";Version=3;");

        public static void openDBConnection()
        {
            if(File.Exists(dbName) == false)
            {
                createDatabase();
            }
            else
            {
                connection.Open();
            }
        }

        public static void closeDBConnection()
        {
            if(connection.State != System.Data.ConnectionState.Closed)
            {
                connection.Close();
            }
        }

        public static int countImages(ImageSource source)
        {
            String sql = "SELECT Count(*) FROM Images " +
                         "INNER JOIN Sources ON Sources.id = Images.sourceId AND Sources.path = @path";
            SQLiteCommand cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.Add(new SQLiteParameter("@path", source.path));
            int number = Convert.ToInt32(cmd.ExecuteScalar());
            cmd.Dispose();
            return number;
        }

        public static List<ImageSource> getUsedSources()
        {
            String sql = "SELECT * FROM Sources Where isUsed = 1";
            SQLiteCommand cmd = new SQLiteCommand(sql, connection);
            using (SQLiteDataReader reader = cmd.ExecuteReader())
            {
                cmd.Dispose();
                List<ImageSource> sourceList = new List<ImageSource>();
                while (reader.Read())
                {
                    ImageSource source = new ImageSource((String)reader["name"], (String)reader["path"], (ImageSource.Type)(Int64)reader["type"], 0, true);
                    source.imageCount = countImages(source);
                    sourceList.Add(source);
                }
                return sourceList;
            }
        }

        public static List<ImageSource> getAllSources()
        {
            String sql = "SELECT * FROM Sources";
            SQLiteCommand cmd = new SQLiteCommand(sql, connection);
            using (SQLiteDataReader reader = cmd.ExecuteReader())
            {
                cmd.Dispose();
                List<ImageSource> sourceList = new List<ImageSource>();
                while (reader.Read())
                {
                    ImageSource source = new ImageSource((String)reader["name"], (String)reader["path"], (ImageSource.Type)(Int64)reader["type"], 0, (bool)reader["isUsed"]);
                    source.imageCount = countImages(source);
                    sourceList.Add(source);
                }
                return sourceList;
            }
        }

        public static List<Image> getImages(List<ImageSource> sources, Color c, int error)
        {
            String sourcePathCondition = "(Sources.path = '" + sources[0].path + "'";
            for(int i = 1; i < sources.Count; i++)
            {
                sourcePathCondition += " OR Sources.path = '" + sources[i].path + "'";
            }
            sourcePathCondition += ")";
            String sql = "SELECT Images.path, red, green, blue FROM Images " +
                         "INNER JOIN Sources ON Sources.id = Images.sourceId AND " + sourcePathCondition +
                         "WHERE (red BETWEEN @redMin AND @redMax) AND " +
                               "(blue BETWEEN @blueMin AND @blueMax) AND " +
                               "(green BETWEEN @greenMin AND @greenMax)";
            SQLiteCommand cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.Add(new SQLiteParameter("@redMin", c.R - error));
            cmd.Parameters.Add(new SQLiteParameter("@greenMin", c.G - error));
            cmd.Parameters.Add(new SQLiteParameter("@blueMin", c.B - error));
            cmd.Parameters.Add(new SQLiteParameter("@redMax", c.R + error));
            cmd.Parameters.Add(new SQLiteParameter("@greenMax", c.G + error));
            cmd.Parameters.Add(new SQLiteParameter("@blueMax", c.B + error));
            using(SQLiteDataReader reader = cmd.ExecuteReader())
            {
                cmd.Dispose();
                List<Image> imageList = new List<Image>();
                while(reader.Read())
                {
                    Color color = Color.FromArgb((int)(Int64)reader["red"], (int)(Int64)reader["green"], (int)(Int64)reader["blue"]);
                    imageList.Add(new Image((String)reader["path"], color));
                }
                return imageList;
            }
        }

        public static void addImage(ImageSource source, String path, Color c, String hashcode)
        {
            int sourceId = getSourceId(source);
            String sql = "INSERT INTO Images (sourceId, path, red, green, blue, hashcode) " +
                         "VALUES (@sourceId, @path, @red, @green, @blue, @hashcode)";
            SQLiteCommand cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.Add(new SQLiteParameter("@sourceId", sourceId));
            cmd.Parameters.Add(new SQLiteParameter("@path", path));
            cmd.Parameters.Add(new SQLiteParameter("@red", c.R));
            cmd.Parameters.Add(new SQLiteParameter("@green", c.G));
            cmd.Parameters.Add(new SQLiteParameter("@blue", c.B));
            cmd.Parameters.Add(new SQLiteParameter("@hashcode", hashcode));
            cmd.ExecuteNonQuery();
            cmd.Dispose();
        }

        public static void addSource(ImageSource source)
        {
            String sql = "INSERT INTO Sources (path, name, type, isUsed) VALUES (@path, @name, @type, @isUsed)";
            SQLiteCommand cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.Add(new SQLiteParameter("@path", source.path));
            cmd.Parameters.Add(new SQLiteParameter("@name", source.name));
            cmd.Parameters.Add(new SQLiteParameter("@type", (int)source.type));
            cmd.Parameters.Add(new SQLiteParameter("@isUsed", source.isUsed));
            cmd.ExecuteNonQuery();
            cmd.Dispose();
        }

        public static bool containsSource(ImageSource source)
        {
            String sql = "SELECT * FROM Sources WHERE path = @path";
            SQLiteCommand cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.Add(new SQLiteParameter("@path", source.path));
            using (SQLiteDataReader reader = cmd.ExecuteReader())
            {
                cmd.Dispose();
                return reader.Read();
            }
        }

        public static void updateIsUsedField(ImageSource source)
        {
            String sql = "UPDATE Sources SET isUsed = @isUsed WHERE path = @path";
            SQLiteCommand cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.Add(new SQLiteParameter("@path", source.path));
            cmd.Parameters.Add(new SQLiteParameter("@isUsed", source.isUsed));
            cmd.ExecuteNonQuery();
            cmd.Dispose();
        }

        public static void removeSource(ImageSource source)
        {
            int sourceId = getSourceId(source);
            String sql = "DELETE FROM Sources WHERE id = @id";
            SQLiteCommand cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.Add(new SQLiteParameter("@id", sourceId));
            cmd.ExecuteNonQuery();
            cmd.Dispose();
            removeImages(sourceId);
        }

        private static void createDatabase()
        {
            SQLiteConnection.CreateFile(dbName);
            connection.Open();
            createTables();
        }

        private static void createTables()
        {
            String sql = "CREATE TABLE Sources (id INTEGER PRIMARY KEY ASC, path TEXT, name TEXT, type INTEGER, isUsed BOOLEAN);";
            SQLiteCommand cmd = new SQLiteCommand(sql, connection);
            cmd.ExecuteNonQuery();
            cmd.Dispose();
            sql = "CREATE TABLE Images (sourceId INTEGER, path TEXT, red INTEGER, green INTEGER, blue INTEGER, hashcode TEXT);";
            cmd = new SQLiteCommand(sql, connection);
            cmd.ExecuteNonQuery();
            cmd.Dispose();
        }

        private static int getSourceId(ImageSource source)
        {
            String sql = "SELECT id FROM Sources WHERE path = @path AND name = @name AND type = @type";
            SQLiteCommand cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.Add(new SQLiteParameter("@path", source.path));
            cmd.Parameters.Add(new SQLiteParameter("@name", source.name));
            cmd.Parameters.Add(new SQLiteParameter("@type", (int)source.type));
            int sourceId = Convert.ToInt32(cmd.ExecuteScalar());
            cmd.Dispose();
            return sourceId;
        }  
      
        private static void removeImages(int sourceId)
        {
            String sql = "DELETE FROM Images WHERE sourceId = @id";
            SQLiteCommand cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.Add(new SQLiteParameter("@id", sourceId));
            cmd.ExecuteNonQuery();
            cmd.Dispose();
        }
    }
}
