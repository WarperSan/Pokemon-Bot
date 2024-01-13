using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.Resources.PokeDatabase;
using File = System.IO.File;

namespace Pokemon_Bot
{
    internal class Bot
    {
        public void MoveToBotCoord(BotCoord start, BotCoord end, Map carte)
        {
            byte[] data = File.ReadAllBytes("C:\\Users\\WarperSan\\Downloads\\4791 - Pokemon - Version Argent SoulSilver (France) [b]_DSPRE_contents\\arm9.bin");
            MemoryStream memoryStream = new MemoryStream(data);

            // Get all warps
            string path = "";

            if (start.Header != end.Header)
                path = FindWarp(start.Header, null, end.Header, memoryStream).path;

            if (string.IsNullOrEmpty(path))
            {
                MoveToPoint(start.LocalPosition, end.LocalPosition, carte);
            }
            else
            {
                string inputs = "";
                string[] sections = (start.Header + "\\" + path).Split('\\');
                Point<short> startPoint = start.LocalPosition;
                Point<short> warpPoint;

                Player player = new Player();
                player.canSurf = false;

                // Get all warps
                for (int i = 0; i < sections.Length; i++)
                {
                    memoryStream.Seek(0xF6BC4 + 0x18 * short.Parse(sections[i]), SeekOrigin.Begin);

                    Header a = new Header(memoryStream);
                    EventFile d = new EventFile(EventFile.DirLocation + a.eventFileID.ToString("D4"));

                    Matrix matrix = new Matrix($"C:\\Users\\WarperSan\\Downloads\\4791 - Pokemon - Version Argent SoulSilver (France) [b]_DSPRE_contents\\unpacked\\matrices\\{a.matrixNumber.ToString("D4")}");

                    Map c = new Map(
                    $"C:\\Users\\WarperSan\\Downloads\\4791 - Pokemon - Version Argent SoulSilver (France) [b]_DSPRE_contents\\unpacked\\maps\\{matrix.GetMapNumber(0, 0).ToString("D4")}",
                        player);
                    //Console.WriteLine(a.matrixNumber);

                    if (i != 0)
                    {
                        startPoint = d.warps.First(x => x.ToHeader == short.Parse(sections[i - 1])).LocalPosition;
                    }

                    //Console.WriteLine(sections[i]);
                    if (i != sections.Length - 1)
                    {
                        warpPoint = d.warps.First(x => x.ToHeader == short.Parse(sections[i + 1])).LocalPosition;
                    }
                    else
                    {
                        warpPoint = end.LocalPosition;
                    }

                    string localInputs = "";



                    try
                    {
                        localInputs = PathFinding.GetInputs(startPoint, warpPoint, c);

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        return;
                    }

                    Console.WriteLine("Path found !");
                    Console.WriteLine(localInputs);
                    Console.WriteLine();

                    inputs += "," + localInputs;
                }

                inputs = inputs.Substring(1, inputs.Length - 1);
                Console.WriteLine(inputs);
                File.WriteAllText("C:\\Users\\WarperSan\\Downloads\\Pokemon Bot\\inputs.dir", inputs);

                // 1. Load map
                // 2. Find warp
                // 3. Get new position if warp taken
                // 4. Get inputs to warp
            }
        }

        private (string path, bool success) FindWarp(ushort currentHeader, ushort? parentHeader, ushort endHeader, MemoryStream memoryStream)
        {
            // 1. Search for warps if not in the same header
            // 2. Move to warp
            // 3. Repeat 1-2 until the same header
            // 4. Retrieve the path

            // Get all warps
            memoryStream.Seek(0xF6BC4 + 0x18 * currentHeader, SeekOrigin.Begin);

            Header a = new Header(memoryStream);
            EventFile d = new EventFile(EventFile.DirLocation + a.eventFileID.ToString("D4"));

            foreach (var b in d.warps)
            {
                if (b.ToHeader != parentHeader)
                {
                    if (b.ToHeader == endHeader)
                        return (b.ToHeader.ToString(), true);

                    (string path, bool success) = FindWarp(b.ToHeader, currentHeader, endHeader, memoryStream);
                    if (success)
                        return (b.ToHeader + "\\" + path, true);
                }
            }
            return ("", false);
        }

        private void MoveToPoint(Point<short> startPos, Point<short> endPos, Map carte)
        {
            string inputs = PathFinding.GetInputs(startPos, endPos, carte);

            Console.WriteLine(inputs);
            File.WriteAllText("C:\\Users\\WarperSan\\Downloads\\Pokemon Bot\\inputs.dir", inputs);
        }

        public struct BotCoord
        {
            public ushort Header;
            public Point<short> LocalPosition;

            public BotCoord(ushort Header, Point<short> LocalPosition)
            {
                this.Header = Header;
                this.LocalPosition = LocalPosition;
            }
        }

        private class PathFinding
        {
            public static string GetInputs(Point<short> startPos, Point<short> endPos, Map carte)
            {
                Tile endTile = carte.tiles[endPos.X + endPos.Y * 32];
                string collisionType = endTile.CollisionType;

                if (carte.GetTile(startPos.X, startPos.Y).IsBlocked)
                {
                    Console.WriteLine("Start point is blocked. Recalculating...");
                    List<Tile> neighborsStart = Tile.RemoveBlockedTiles(carte.GetNeighbors(startPos));

                    if (neighborsStart.Count != 0)
                    {
                        startPos = neighborsStart[0].LocalPosition;
                    }
                }

                if (carte.GetTile(endPos.X, endPos.Y).IsBlocked)
                {
                    Console.WriteLine("Warp point is blocked. Recalculating...");
                    List<Tile> neighborsEnd = Tile.RemoveBlockedTiles(carte.GetNeighbors(endPos));

                    if (neighborsEnd.Count != 0)
                    {
                        endPos = neighborsEnd[0].LocalPosition;
                    }
                }

                PathFindingTile lastTile = FindPath(startPos, endPos, carte);
                List<Direction> directions = GetDirections(lastTile);

                if (lastTile == null)
                    throw new Exception($"End Position unreachable");

                if (endTile.IsWarp)
                {
                    if (collisionType.Contains("Left"))
                    {
                        directions.Add(Direction.left);
                    }
                    else if (collisionType.Contains("Down"))
                    {
                        directions.Add(Direction.down);
                    }
                    else if (collisionType.Contains("Door"))
                    {
                        directions.Add(Direction.up);
                    }
                }

                string[] rawInputs = DirectionsToInputConvert(directions);
                return Convert(rawInputs) + (endTile.IsWarp ? ",10 wait" : "");
            }

            public enum Direction
            {
                left,
                right,
                up,
                down,
            }

            class PathFindingTile
            {
                public Point<short> Position = Point<short>.Zero;
                public int Cost { get; set; }
                public int Distance { get; set; }
                public int CostDistance => Cost + Distance;
                public PathFindingTile Parent { get; set; }

                public PathFindingTile() { }

                public PathFindingTile(Point<short> Position)
                {
                    this.Position = Position;
                }

                //The distance is essentially the estimated distance, ignoring walls to our target. 
                //So how many tiles left and right, up and down, ignoring walls, to get there. 
                public void SetDistance(Point<short> target)
                {
                    this.Distance = Math.Abs(target.X - Position.X) + Math.Abs(target.Y - Position.Y);
                }
            }

            static PathFindingTile FindPath(Point<short> startPos, Point<short> endPos, Map carte)
            {
                carte.Encapsulation();
                bool[,] map = carte.Conversion();

                var start = new PathFindingTile(startPos);
                var finish = new PathFindingTile(endPos);

                start.SetDistance(finish.Position);

                var activeTiles = new List<PathFindingTile>();
                activeTiles.Add(start);
                var visitedTiles = new List<PathFindingTile>();

                while (activeTiles.Any())
                {
                    var checkTile = activeTiles.OrderBy(x => x.Cost).First();

                    if (checkTile.Position == finish.Position)
                        return checkTile;

                    visitedTiles.Add(checkTile);
                    activeTiles.Remove(checkTile);

                    var walkableTiles = GetWalkableTiles(map, checkTile, finish, carte);

                    foreach (var walkableTile in walkableTiles)
                    {
                        //We have already visited this tile so we don't need to do so again!
                        if (visitedTiles.Any(x => x.Position == walkableTile.Position))
                            continue;

                        //It's already in the active list, but that's OK, maybe this new tile has a better value (e.g. We might zigzag earlier but this is now straighter). 
                        if (activeTiles.Any(x => x.Position == walkableTile.Position))
                        {
                            var existingTile = activeTiles.First(x => x.Position == walkableTile.Position);
                            if (existingTile.CostDistance > checkTile.CostDistance)
                            {
                                activeTiles.Remove(existingTile);
                                activeTiles.Add(walkableTile);
                            }
                        }
                        else
                        {
                            //We've never seen this tile before so add it to the list. 
                            activeTiles.Add(walkableTile);
                        }
                    }
                }
                return null;
            }

            static List<PathFindingTile> GetWalkableTiles(bool[,] map, PathFindingTile currentTile, PathFindingTile targetTile, Map carte)
            {
                var possibleTiles = new List<PathFindingTile>();

                for (int y = -1; y < 2; y += 2)
                {
                    Point<short> pos = new Point<short>(currentTile.Position.X, (short)(currentTile.Position.Y + y));
                    possibleTiles.Add(new PathFindingTile { Position = pos, Parent = currentTile, Cost = carte.GetTileCost(pos) });
                }

                for (int x = -1; x < 2; x += 2)
                {
                    Point<short> pos = new Point<short>((short)(currentTile.Position.X + x), currentTile.Position.Y);
                    possibleTiles.Add(new PathFindingTile { Position = pos, Parent = currentTile, Cost = carte.GetTileCost(pos) });
                }

                possibleTiles.ForEach(tile => tile.SetDistance(targetTile.Position));

                var maxX = map.GetLength(1) - 1;
                var maxY = map.GetLength(0) - 1;

                return possibleTiles
                        .Where(tile => tile.Position.X >= 0 && tile.Position.X <= maxX)
                        .Where(tile => tile.Position.Y >= 0 && tile.Position.Y <= maxY)
                        .Where(tile => map[tile.Position.Y, tile.Position.X] || tile.Position == new Point<short>(3, 0))
                        .ToList();
            }

            static List<Direction> GetDirections(PathFindingTile tile)
            {
                if (tile == null)
                    return new List<Direction>();

                // Trace back the trail of steps
                Stack<Point<short>> traveled = new Stack<Point<short>>();
                while (true)
                {
                    traveled.Push(tile.Position);

                    // End of the trail
                    if (tile.Parent == null)
                        break;

                    tile = tile.Parent;
                }

                Point<short>[] traveledC = new Point<short>[traveled.Count];
                traveled.CopyTo(traveledC, 0);

                int size = traveled.Count;
                Point<short> init;
                Point<short> end;
                List<Direction> directions = new List<Direction>();

                for (int i = 0; i < size - 1; i++)
                {
                    init = traveled.Pop();
                    end = traveled.Peek();

                    // Calculate direction
                    Point<short> result = init - end;

                    directions.Add(GetDirection(result));
                }
                return directions;
            }

            public static Direction GetDirection(Point<short> vector)
            {
                if (vector.X >= 1)
                    return Direction.left;
                else if (vector.X <= -1)
                    return Direction.right;
                else if (vector.Y >= 1)
                    return Direction.up;
                return Direction.down;
            }

            static string[] DirectionsToInputConvert(List<Direction> directions)
            {
                List<string> inputs = new List<string>();

                for (int i = 0; i < directions.Count; i++)
                {
                    inputs.Add(directions[i].ToString());
                }
                return inputs.ToArray();
            }

            static string Convert(string[] inputs)
            {
                string convertedInputs = "";

                if (inputs.Length != 0)
                {
                    string currentDir = inputs[0];

                    uint count = 0;


                    for (int i = 0; i < inputs.Length; i++)
                    {
                        if (inputs[i] == currentDir)
                        {
                            count++;
                        }
                        else
                        {
                            if (count != 0)
                                convertedInputs += count + " " + currentDir + ",";
                            currentDir = inputs[i];
                            count = 0;
                            convertedInputs += currentDir + ",";
                        }
                    }

                    if (count > 0)
                        convertedInputs += count + " " + currentDir;
                    else
                        convertedInputs = convertedInputs.Substring(0, convertedInputs.Length - 1);
                }
                return convertedInputs;
            }
        }

        public class EventFile
        {
            public const string DirLocation = "C:\\Users\\WarperSan\\Downloads\\4791 - Pokemon - Version Argent SoulSilver (France) [b]_DSPRE_contents\\unpacked\\eventFiles\\";

            public Spawnable[] spawnables;
            public Overworld[] overworlds;
            public Warp[] warps;
            public Trigger[] triggers;

            public EventFile(string path)
            {
                byte[] data = File.ReadAllBytes(path);

                using (BinaryReader reader = new BinaryReader(new MemoryStream(data)))
                {
                    // Spawnables
                    ulong spawnablesCount = reader.ReadUInt32();

                    spawnables = new Spawnable[spawnablesCount];
                    for (ulong i = 0; i < spawnablesCount; i++)
                    {
                        spawnables[i] = new Spawnable(reader.ReadBytes(20));
                    }

                    // Overworlds
                    ulong overworldsCount = reader.ReadUInt32();

                    overworlds = new Overworld[overworldsCount];
                    for (ulong i = 0; i < overworldsCount; i++)
                    {
                        overworlds[i] = new Overworld(reader.ReadBytes(0x20));
                    }

                    // Warps
                    ulong warpsCount = reader.ReadUInt32();

                    warps = new Warp[warpsCount];
                    for (ulong i = 0; i < warpsCount; i++)
                    {
                        warps[i] = new Warp(reader.ReadBytes(0xC));
                    }
                }

                /*
                // Triggers
                int triggersCount = int.Parse(GetHexValue(data, cursorPos, 4), NumberStyles.HexNumber);
                cursorPos += 4;

                triggers = new Trigger[triggersCount];
                for (int i = 0; i < triggersCount; i++)
                {
                    triggers[i] = new Trigger(GetHexValue(data, cursorPos, Trigger.Size));
                    Console.WriteLine(triggers[i]);
                    cursorPos += Trigger.Size;
                }*/
            }

            public class Spawnable
            {
                #region Proprities
                public ushort ScriptID;
                public Point<uint> Position = Point<uint>.Zero;
                public string SpawnType;
                public string ActivCriteria;
                #endregion

                public Spawnable(byte[] data)
                {
                    using (BinaryReader reader = new BinaryReader(new MemoryStream(data)))
                    {
                        // 0x0-1
                        ScriptID = reader.ReadUInt16();

                        // 0x2-3
                        reader.ReadUInt16();

                        // 0x4-7
                        Position.X = reader.ReadUInt32();

                        // 0x8-B
                        Position.Y = reader.ReadUInt32();

                        // 0xC-F
                        SpawnType = EventEditor.Spawnables.typesArray[reader.ReadUInt32()];

                        // 0x10-13
                        ActivCriteria = EventEditor.Spawnables.orientationsArray[reader.ReadUInt32()];
                    }
                }

                public override string ToString()
                {
                    return $"Spawnable: \nScript #{ScriptID}\n{Position}\nType: {SpawnType}\nActivation Criteria: {ActivCriteria}";
                }
            }

            public class Overworld
            {
                #region Proprities
                public ushort ID;
                public ushort Entry;
                public string Movement;
                public Point<ushort> MovementRange;
                public ushort Flag;
                public ushort Script;
                public ushort SightRange;
                public Point<ushort> Position;
                #endregion

                public Overworld(byte[] data)
                {
                    using (BinaryReader reader = new BinaryReader(new MemoryStream(data)))
                    {
                        // 0x0-1
                        ID = reader.ReadUInt16();

                        // 0x2-3
                        Entry = reader.ReadUInt16();

                        // 0x4-5
                        ushort result = reader.ReadUInt16();
                        Movement = EventEditor.Overworlds.movementsArray[result];

                        // 0x6-7
                        reader.ReadUInt16();

                        // 0x8-9
                        Flag = reader.ReadUInt16();

                        // 0xA-B
                        Script = reader.ReadUInt16();

                        // 0xC-D (First Orientation)
                        reader.ReadUInt16();

                        // 0xE-F
                        SightRange = reader.ReadUInt16();

                        // 0x10-11
                        reader.ReadUInt16();

                        // 0x12-13
                        reader.ReadUInt16();

                        // 0x14-15
                        MovementRange.X = reader.ReadUInt16();

                        // 0x16-17
                        MovementRange.Y = reader.ReadUInt16();

                        // 0x18-19
                        Position.X = reader.ReadUInt16();

                        // 0x1A-1B
                        Position.Y = reader.ReadUInt16();

                        // 0x1C-1D
                        Position.Z = reader.ReadUInt16();

                        // 0x1E-1F
                        reader.ReadUInt16();
                    }
                }

                public override string ToString()
                {
                    return $"ID: {ID}\nEntry: {Entry}\nMovement Type: {Movement}\nMovement Range: {MovementRange}\nFlag: {Flag}\nScript: {Script}\nSight Range: {SightRange}\n{Position}";
                }
            }

            public class Warp
            {
                #region Proprities
                public Point<short> Position;
                public ushort ToHeader;
                public ushort Hook;
                #endregion

                public Point<short> LocalPosition => new Point<short>(
                    (short)(Position.X - Position.X / 32 * 32),
                    (short)(Position.Y - Position.Y / 32 * 32));

                public Warp(byte[] data)
                {
                    using (BinaryReader reader = new BinaryReader(new MemoryStream(data)))
                    {
                        // 0x0-1
                        Position.X = reader.ReadInt16();

                        // 0x2-3
                        Position.Y = reader.ReadInt16();

                        // 0x4-5
                        ToHeader = reader.ReadUInt16();

                        // 0x6-7
                        Hook = reader.ReadUInt16();

                        // 0x8-9
                        reader.ReadUInt16();

                        // 0xA-B
                        reader.ReadUInt16();
                    }
                }

                public override string ToString()
                {
                    return $"To Header #{ToHeader}\nHook: {Hook}\n{Position}";
                }
            }

            public class Trigger
            {
                #region Constantes
                public static int Size => 0x10;
                #endregion

                #region Proprities
                public ushort Script;
                public Point<ushort> Position;
                public ushort Width;
                public ushort Length;
                public ushort ExpectedValue;
                public ushort VariableWatched;
                #endregion

                public Trigger(string hexValue)
                {
                    /*if (hexValue.Length != Size * 2)
                        throw new Exception("Invalid string size");

                    // 0xE-F
                    VariableWatched = ushort.Parse(GetPortionAndModify(ref hexValue, 0, 4), NumberStyles.HexNumber);

                    // 0xC-D
                    ExpectedValue = ushort.Parse(GetPortionAndModify(ref hexValue, 0, 4), NumberStyles.HexNumber);

                    // 0xA-B
                    GetPortionAndModify(ref hexValue, 0, 4);

                    // 0x8-9
                    Length = ushort.Parse(GetPortionAndModify(ref hexValue, 0, 4), NumberStyles.HexNumber);

                    // 0x6-7
                    Width = ushort.Parse(GetPortionAndModify(ref hexValue, 0, 4), NumberStyles.HexNumber);

                    // 0x4-5
                    Position.Y = ushort.Parse(GetPortionAndModify(ref hexValue, 0, 4), NumberStyles.HexNumber);

                    // 0x2-3
                    Position.X = ushort.Parse(GetPortionAndModify(ref hexValue, 0, 4), NumberStyles.HexNumber);

                    // 0x0-1
                    Script = ushort.Parse(GetPortionAndModify(ref hexValue, 0, 4), NumberStyles.HexNumber);*/
                }

                public override string ToString()
                {
                    return $"Script: {Script}\n{Position}\nWidth: {Width}\nLength: {Length}\nExpected Value: {ExpectedValue}\nVariable Watched: {VariableWatched}";
                }
            }
        }

        public class Trainer
        {
            public class TrainerProperties
            {
                public const int AI_COUNT = 11;
                public const int TRAINER_ITEMS = 4;

                #region Fields
                public ushort trainerID;
                public byte trDataUnknown;

                public byte trainerClass = 0;
                public byte partyCount = 0;

                public bool doubleBattle = false;
                public bool hasMoves = false;
                public bool hasItems = false;

                public ushort[] trainerItems = new ushort[TRAINER_ITEMS];
                public BitArray AI;
                #endregion

                public TrainerProperties(Stream trainerPropertiesStream)
                {
                    using (BinaryReader reader = new BinaryReader(trainerPropertiesStream))
                    {
                        byte flags = reader.ReadByte();
                        hasMoves = (flags & 1) != 0;
                        hasItems = (flags & 2) != 0;
                        
                        trainerClass = reader.ReadByte();
                        trDataUnknown = reader.ReadByte();
                        partyCount = reader.ReadByte();

                        for (int i = 0; i < trainerItems.Length; i++)
                        {
                            trainerItems[i] = reader.ReadUInt16();
                        }

                        AI = new BitArray(BitConverter.GetBytes(reader.ReadUInt32()));
                        doubleBattle = reader.ReadUInt32() == 2;
                    }
                }
            }
        }
    }
}
