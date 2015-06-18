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
        public const String dbName = "IndexedImageFolders.db";
        private static Int64 directoryId;
        private static SQLiteConnection connection = new SQLiteConnection("DataSource=" + dbName + ";Version=3;");

        public static void databaseCheck()
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

        public static List<Image> getImages(String imageDirectoryPath, Color c, int error)
        {
            String sql = "SELECT name, red, green, blue FROM Images " +                         
                         "INNER JOIN Folders ON Folders.id = Images.folderId AND Folders.path = @path " +
                         "WHERE (red BETWEEN @redMin AND @redMax) AND " +
                               "(blue BETWEEN @blueMin AND @blueMax) AND " +
                               "(green BETWEEN @greenMin AND @greenMax)";
            SQLiteCommand cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.Add(new SQLiteParameter("@path", imageDirectoryPath));
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
                    imageList.Add(new Image(imageDirectoryPath + '\\' + (String)reader["name"], color));
                }
                return imageList;
            }
        }

        public static void addImage(String name, Color c, String hashcode)
        {
            String sql = "INSERT INTO Images (folderId, name, red, green, blue, hashcode) " +
                         "VALUES (@folderId, @name, @red, @green, @blue, @hashcode)";
            SQLiteCommand cmd = new SQLiteCommand(sql, connection);            
            cmd.Parameters.Add(new SQLiteParameter("@folderId", directoryId));
            cmd.Parameters.Add(new SQLiteParameter("@name", name));
            cmd.Parameters.Add(new SQLiteParameter("@red", c.R));
            cmd.Parameters.Add(new SQLiteParameter("@green", c.G));
            cmd.Parameters.Add(new SQLiteParameter("@blue", c.B));
            cmd.Parameters.Add(new SQLiteParameter("@hashcode", hashcode));
            cmd.ExecuteNonQuery();
            cmd.Dispose();
        }

        public static void addDirectory(String directoryPath)
        {
            String sql = "INSERT INTO Folders (path) VALUES (@path)";
            SQLiteCommand cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.Add(new SQLiteParameter("@path", directoryPath));
            cmd.ExecuteNonQuery();
            sql = "SELECT last_insert_rowid()";
            cmd.Dispose();
            cmd = new SQLiteCommand(sql, connection);
            directoryId = (Int64)cmd.ExecuteScalar();
            cmd.Dispose();
        }

        public static bool directoryRecorded(String directoryPath)
        {
            String sql = "SELECT * FROM Folders WHERE path = @path";
            SQLiteCommand cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.Add(new SQLiteParameter("@path", directoryPath));
            using (SQLiteDataReader reader = cmd.ExecuteReader())
            {
                cmd.Dispose();
                if (reader.Read())
                {
                    directoryId = (Int64)reader["id"];
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private static void createDatabase()
        {
            SQLiteConnection.CreateFile(dbName);
            connection.Open();
            createTables();
        }

        private static void createTables()
        {
            String sql = "CREATE TABLE Folders (id INTEGER PRIMARY KEY ASC, path TEXT);";
            SQLiteCommand cmd = new SQLiteCommand(sql, connection);
            cmd.ExecuteNonQuery();
            cmd.Dispose();
            sql = "CREATE TABLE Images (folderId INTEGER, name TEXT, red INTEGER, green INTEGER, blue INTEGER, hashcode TEXT);";
            cmd = new SQLiteCommand(sql, connection);
            cmd.ExecuteNonQuery();
            cmd.Dispose();
        }
    }
}
