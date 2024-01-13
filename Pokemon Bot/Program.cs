using DSPRE.Resources;
using System;
using System.Collections.Generic;
using System.IO;

namespace Pokemon_Bot
{
    // Load Matrix
    // Get Map #
    // Load Map
    // Get Tiles

    // Encapsulation of the map (if tile has 3 blocked tiles near it => blocked)


    enum Direction
    {
        Left = 0x00,
        Right = 0x01,
        Up = 0x10,
        Down = 0x11
    }

    struct Point<T>
    {
        public T X { get; set; }
        public T Y { get; set; }
        public T Z { get; set; }

        public Point(T X, T Y, T Z = default(T))
        {
            this.X = X;
            this.Y = Y;
            this.Z = Z;
        }

        public static Point<T> Zero => new Point<T>(default(T), default(T));

        public override string ToString()
        {
            return $"Point {{{X}; {Y}; {Z}}}";
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(Point<T> c1, Point<T> c2)
        {
            return c1.X.Equals(c2.X) && c1.Y.Equals(c2.Y) && c1.Z.Equals(c2.Z);
        }

        public static bool operator !=(Point<T> c1, Point<T> c2)
        {
            return !(c1 == c2);
        }

        public static Point<T> operator -(Point<T> c1, Point<T> c2)
        {
            return new Point<T>(
                (T)(c1.X as dynamic - c2.X as dynamic),
                (T)(c1.Y as dynamic - c2.Y as dynamic),
                (T)(c1.Z as dynamic - c2.Z as dynamic)
                );
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            Bot.BotCoord start = new Bot.BotCoord(64, new Point<short>(2, 9));
            Bot.BotCoord end = new Bot.BotCoord(384, new Point<short>(2, 9));

            // Load current map
            // Go to warp
            // Go to position

            //string a = $"C:\\Users\\WarperSan\\Downloads\\4791 - Pokemon - Version Argent SoulSilver (France) [b]_DSPRE_contents\\unpacked\\matrices\\0002";

            //Matrix matrix = new Matrix(a);
            //Console.WriteLine(matrix.GetMapNumber(0, 0));

            Player player = new Player();
            player.canSurf = false;

            Map carte = new Map(
                "C:\\Users\\WarperSan\\Downloads\\4791 - Pokemon - Version Argent SoulSilver (France) [b]_DSPRE_contents\\unpacked\\maps\\0217",
                player);

            Bot bot = new Bot();
            bot.MoveToBotCoord(start, end, carte);
        }

        public static void UnpackFile(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            string text = "";

            for (int i = 0; i < data.Length; i++)
            {
                if (i % 16 == 0 && i != 0)
                    text += "\n";

                text += data[i].ToString("X2") + " ";
            }
            File.WriteAllText(path + "-unpacked.txt", text);
        }

        public static string GetHexValue(byte[] data, int start, int size, bool readInReverse = true)
        {
            string hexValue = "";
            if (readInReverse)
            {
                for (int i = size - 1; i >= 0; i--)
                {
                    hexValue += data[start + i].ToString("X2");
                }
            }
            else
            {
                for (int i = 0; i < size; i++)
                {
                    hexValue += data[start + i].ToString("X2");
                }
            }
            return hexValue;
        }

        public static string GetPortionAndModify(ref string value, int start, int length)
        {
            string result = value.Substring(start, length);
            value = value.Substring(start + length);
            return result;
        }
    }

    class Tile
    {
        #region Properties
        public string Collision;
        public string CollisionType;
        public Point<short> LocalPosition;
        #endregion

        #region Automatic Properties
        public bool IsBlocked
        {
            get => Collision == "[80] Blocked";
            set
            {
                Collision = value ? "[80] Blocked" : PokeDatabase.System.MapCollisionPainters[0];
            }
        }

        public bool IsWater
        {
            get => CollisionType.Contains("Water");
            set
            {
                CollisionType = value ? "[10] River Water (Wild)" : PokeDatabase.System.MapCollisionPainters[0];
            }
        }

        public bool IsWarp
        {
            get => CollisionType.Contains("Warp");
        }

        #endregion
        public Tile(ushort hexValue, Point<short> LocalPosition)
        {
            this.LocalPosition = LocalPosition;

            // 0x0
            CollisionType = PokeDatabase.System.MapCollisionTypePainters[(byte)hexValue];

            // 0x1
            // Because for some reason it doesn't export correctly
            try
            {
                Collision = PokeDatabase.System.MapCollisionPainters[(byte)(hexValue >> 8)];
            }
            catch (Exception)
            {
                Collision = PokeDatabase.System.MapCollisionPainters[0];
            }
        }

        public static List<Tile> RemoveBlockedTiles(List<Tile> tiles)
        {
            for (int i = tiles.Count - 1; i >= 0; i--)
            {
                if (tiles[i].IsBlocked)
                    tiles.RemoveAt(i);
            }
            return tiles;
        }
    }

    class Matrix
    {
        byte Width;
        byte Height;
        int[] Data;

        public Matrix(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException();

            byte[] data = File.ReadAllBytes(path);
            Width = data[0];
            Height = data[1];
            Data = new int[Width * Height];
            long headStart;
            int size = 2;

            //if (data[2] == 0x1)
            //    headStart += Data.Length * size; // Headers

            //if (data[3] == 0x1)
            //    headStart += Data.Length; // Heights

            MemoryStream memoryStream = new MemoryStream(data);
            headStart = memoryStream.Length - Width * Height * size;

            memoryStream.Seek(headStart, SeekOrigin.Begin);

            BinaryReader reader = new BinaryReader(memoryStream);

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                ushort result = reader.ReadUInt16();

                switch (result)
                {
                    case 0xFFFF:
                    case 0x00D0:
                    case 0x00D1:
                    case 0x00D2:
                        result = 0x0000;
                        break;
                    default:
                        break;
                }

                Data[(reader.BaseStream.Position - headStart) / size - 1] = result;
            }
        }

        public int GetMapNumber(int X, int Y)
        {
            return Data[X + Y * Width];
        }
    }

    class Map
    {
        public const int Height = 32;
        public const int Width = 32;

        public Tile[] tiles = new Tile[Height * Width];
        Player player;

        public Map(string path, Player player)
        {
            if (!File.Exists(path)) throw new FileNotFoundException();

            byte[] data = File.ReadAllBytes(path);

            Stream stream = new MemoryStream(data);
            stream.Position += 0x12;

            using (BinaryReader reader = new BinaryReader(stream))
            {
                reader.BaseStream.Position += reader.ReadByte() + 2;

                for (short i = 0; i < Height; i++)
                {
                    for (short j = 0; j < Width; j++)
                    {
                        tiles[i * Width + j] = new Tile(reader.ReadUInt16(), new Point<short>(j, i));
                    }
                }
            }


            this.player = player;
        }

        public void Encapsulation()
        {
            Point<short> latestNotBlocked = new Point<short>(-1, -1);

            for (short i = 0; i < Height; i++)
            {
                for (short j = 0; j < Width; j++)
                {
                    if (tiles[i * Width + j].IsBlocked)
                        continue;

                    Point<short> position = new Point<short>(j, i);
                    List<Tile> neighbors = GetNeighbors(position);

                    for (int k = neighbors.Count - 1; k >= 0; k--)
                    {
                        if (!neighbors[k].IsBlocked)
                            neighbors.RemoveAt(k);
                    }


                    if (neighbors.Count >= 3)
                    {
                        tiles[i * Width + j].IsBlocked = true;

                        if (latestNotBlocked.X != -1)
                        {
                            latestNotBlocked.X--;

                            if (latestNotBlocked.X < 0)
                                latestNotBlocked.X = 0;

                            i = latestNotBlocked.Y;
                            j = latestNotBlocked.X;
                        }
                    }
                    else
                    {
                        latestNotBlocked = position;
                    }
                }
            }
        }

        public List<Tile> GetNeighbors(Point<short> position)
        {
            List<Tile> neighbors = new List<Tile>();

            if (position.X != 0)
            {
                neighbors.Add(tiles[position.Y * Width + position.X - 1]);
            }

            if (position.X != Width - 1)
            {
                neighbors.Add(tiles[position.Y * Width + position.X + 1]);
            }

            if (position.Y != 0)
            {
                neighbors.Add(tiles[(position.Y - 1) * Width + position.X]);
            }

            if (position.Y != Height - 1)
            {
                neighbors.Add(tiles[(position.Y + 1) * Width + position.X]);
            }

            return neighbors;
        }

        public bool[,] Conversion()
        {
            bool[,] newMap = new bool[Height, Width];

            for (int i = 0; i < tiles.Length; i++)
            {
                bool isBlocked = tiles[i].IsBlocked || (!player.canSurf && tiles[i].IsWater);

                newMap[i / Width, i - i / Width * Width] = !isBlocked;
            }

            return newMap;
        }

        public int GetTileCost(Point<short> position)
        {
            int cost = 0;

            if (position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height)
            {
                string type = tiles[position.Y * Width + position.X].CollisionType;

                if (type.Contains("Wild"))
                    cost = 2;
            }

            return cost;
        }

        public Tile GetTile(int X, int Y)
        {
            return tiles[X + Y * Width];
        }
    }

    class Header
    {
        byte wildPokemonFile; // 0x0
        byte areaData; // 0x1
        public ushort matrixNumber; // 0x4
        private ushort scriptFileID;
        private ushort levelScriptID;
        private ushort textArchiveID;
        private ushort musicDayID;
        private ushort musicNightID;
        public ushort eventFileID;
        private byte locationName;
        public Point<byte> Position = Point<byte>.Zero;

        /*
0x13 //  byte:       Map name textbox type value
0x14 //  byte:       Weather value
0x15 //  byte:       Camera value
0x16 //  byte:       Follow mode (for the Pokémon following hero)
0x17 //  byte:       Bitwise permission flags:
HGSS
-----------------    1: ?
-----------------    2: ?
-----------------    3: ?
-----------------    4: Allow Fly 
-----------------    5: Allow Esc.Rope
-----------------    6: ?
-----------------    7: Allow Bicycle
-----------------    8: ?*/

        public Header(Stream data)
        {
            BinaryReader reader = new BinaryReader(data);

            wildPokemonFile = reader.ReadByte();
            areaData = reader.ReadByte();

            ushort coords = reader.ReadUInt16();
            /*unknown0 = (byte)(coords & 0b_1111); //get 4 bits*/
            Position.X = (byte)((coords >> 4) & 0b_1111_11); //get 6 bits after the first 4
            Position.Y = (byte)((coords >> 10) & 0b_1111_11); //get 6 bits after the first 10

            matrixNumber = reader.ReadUInt16();
            scriptFileID = reader.ReadUInt16();
            levelScriptID = reader.ReadUInt16();
            textArchiveID = reader.ReadUInt16();
            musicDayID = reader.ReadUInt16();
            musicNightID = reader.ReadUInt16();
            eventFileID = reader.ReadUInt16();
            locationName = reader.ReadByte();

            byte areaProperties = reader.ReadByte();
            //areaIcon = (byte)(areaProperties & 0b_1111); //get 4 bits
            //unknown1 = (byte)((areaProperties >> 4) & 0b_1111); //get 4 bits after the first 4

            uint last32 = reader.ReadUInt32();
            //kantoFlag = (last32 & 0b_1) == 1; //get 1 bit
            //weatherID = (byte)((last32 >> 1) & 0b_1111_111); //get 7 bits after the first one
            //locationType = (byte)((last32 >> 8) & 0b_1111); //get 4 bits after the first 8
            //cameraAngleID = (byte)((last32 >> 12) & 0b_1111_11); //get 6 bits after the first 12
            //followMode = (byte)((last32 >> 18) & 0b_11); //get 2 bits after the first 17
            //battleBackground = (byte)((last32 >> 20) & 0b_1111_1); //get 5 bits after the first 19
            //flags = (byte)(last32 >> 25 & 0b_1111_111); //get 7 bits after the first 24*/
        }
    }

    class Player
    {
        public Point<ushort> AreaPos = new Point<ushort>(21, 12);
        public Point<ushort> SubPos = Point<ushort>.Zero;

        public bool canSurf = false;

        public void MoveArea(Direction moveDirection)
        {
            ushort deltaX = 0;
            ushort deltaY = 0;

            switch (moveDirection)
            {
                case Direction.Left:
                    deltaX--;
                    break;
                case Direction.Right:
                    deltaX++;
                    break;
                case Direction.Up:
                    deltaY--;
                    break;
                case Direction.Down:
                    deltaY++;
                    break;
            }

            Point<ushort> newAreaPos = new Point<ushort>((ushort)(AreaPos.X + deltaX), (ushort)(AreaPos.Y + deltaY));
            AreaPos = newAreaPos;
        }
    }
}
