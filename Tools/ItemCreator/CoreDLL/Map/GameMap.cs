﻿using System;
using System.Collections.Generic;
using System.IO;
using ItemCreator.CoreDLL.Map.MapObj;

namespace ItemCreator.CoreDLL.Map
{
    public unsafe class GameMap
    {
        public const int MaxPath = 260;

        private const int LAYER_NONE = 0;
        private const int LAYER_TERRAIN = 1;
        private const int LAYER_FLOOR = 2;
        private const int LAYER_INTERACTIVE = 3;
        private const int LAYER_SCENE = 4;
        private const int LAYER_SKY = 5;
        private const int LAYER_SURFACE = 6;

        private const int MAP_NONE = 0;
        private const int MAP_TERRAIN = 1;
        private const int MAP_TERRAIN_PART = 2;
        private const int MAP_SCENE = 3;
        private const int MAP_COVER = 4;
        private const int MAP_ROLE = 5;
        private const int MAP_HERO = 6;
        private const int MAP_PLAYER = 7;
        private const int MAP_PUZZLE = 8;
        private const int Map_3Dsimple = 9;
        private const int Map_3Deffect = 10;
        private const int Map_2Ditem = 11;
        private const int Map_3Dnpc = 12;
        private const int Map_3Dobj = 13;
        private const int Map_3Dtrace = 14;
        private const int MAP_SOUND = 15;
        private const int Map_2Dregion = 16;
        private const int Map_3Dmagicmapitem = 17;
        private const int Map_3Ditem = 18;
        private const int Map_3Deffectnew = 19;

        private MySize _mapSize;
        private Cell[,] _cells; //A Cell constains only useful data.
        private readonly Dictionary<int, PassageInfo> _passages;
        private readonly List<RegionInfo> _regions;
        private readonly string _folder;

        public GameMap(string path)
        {
            _mapSize = new MySize();
            _cells = new Cell[0, 0];
            _passages = new Dictionary<int, PassageInfo>();
            _regions = new List<RegionInfo>();

            _folder = Path.GetDirectoryName(path) + "\\..\\..\\";
            LoadDataMap(path);
        }

        public int Width => _mapSize.Width;
        public int Height => _mapSize.Height;

        public Cell? GetCell(int x, int y)
        {
            if (x < 1 || y < 1)
                return null;

            if (x > _mapSize.Width || y > _mapSize.Height)
                return null;

            return _cells[x - 1, y - 1];
        }

        private void LoadDataMap(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException();

            //Load the DMap file...
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                uint version; //Currently 1004?
                uint data;

                stream.Read(&version, sizeof(uint));
                stream.Read(&data, sizeof(uint));
                //Console.WriteLine("Version: {0} Data: {1}", Version, Data);

                byte* pFileName = stackalloc byte[MaxPath];
                stream.Read(pFileName, MaxPath);

                var fullFileName = _folder + new string((sbyte*)pFileName);
                //LoadPuzzle(FullFileName);
                //Console.WriteLine("Puzzle: {0} [{1}]", FullFileName, MAX_PATH);

                fixed (MySize* pMapSize = &_mapSize)
                    stream.Read(pMapSize, sizeof(MySize));
                //Console.WriteLine("Width = {0}, Height = {1}", MapSize.Width, MapSize.Height);

                _cells = new Cell[_mapSize.Width, _mapSize.Height];
                for (var i = 0; i < _mapSize.Height; i++)
                {
                    uint checkData = 0;
                    for (var j = 0; j < _mapSize.Width; j++)
                    {
                        var layer = new LayerInfo();

                        stream.Read(&layer.Mask, sizeof(ushort));
                        stream.Read(&layer.Terrain, sizeof(ushort));
                        stream.Read(&layer.Altitude, sizeof(short));
                        checkData += (uint)(layer.Mask * (layer.Terrain + i + 1) +
                                              (layer.Altitude + 2) * (j + 1 + layer.Terrain));

                        //Console.WriteLine("Cell({0}, {1}) MASK[{2}] TERRAIN[{3}] ALTITUDE[{4}]", j, i, Layer.Mask, Layer.Terrain, Layer.Altitude);
                        _cells[j, i] = new Cell(layer);
                    }
                    uint mapCheckData;
                    stream.Read(&mapCheckData, sizeof(uint));

                    if (mapCheckData != checkData)
                        throw new Exception("Map checksum failed!");
                }

                LoadDataPassage(stream);
                /*if (Version == 1003)
                    LoadDataRegion(Stream);*/
                LoadDataLayer(stream);

                //The rest are LAYER_SCENE, but useless for a server. I'll not implement the rest as it would only
                //slow down the loading.
            }  
        }

        private void LoadDataPassage(FileStream stream)
        {
            if (stream == null)
                throw new ArgumentNullException();

            int amount;
            stream.Read(&amount, sizeof(int));

            for (var i = 0; i < amount; i++)
            {
                PassageInfo passage;
                stream.Read(&passage, sizeof(PassageInfo));

                //Console.WriteLine("Passage[{0}] ({1}, {2}", Passage.Index, Passage.PosX, Passage.PosY);
                _passages.Add(passage.Index, passage);
            }
        }

        private void LoadDataRegion(FileStream stream)
        {
            if (stream == null)
                throw new ArgumentNullException();

            int amount;
            stream.Read(&amount, sizeof(int));

            for (var i = 0; i < amount; i++)
            {
                RegionInfo region;
                stream.Read(&region, sizeof(RegionInfo));
                _regions.Add(region);
            }
        }

        private void LoadDataLayer(FileStream stream)
        {
            if (stream == null)
                throw new ArgumentNullException();

            int amount;
            stream.Read(&amount, sizeof(int));

            for (var i = 0; i < amount; i++)
            {
                int mapObjType;
                stream.Read(&mapObjType, sizeof(int));

                switch (mapObjType)
                {
                    case MAP_COVER:
                        {
                            //Do nothing with it...
                            stream.Seek(sizeof(C2DMapCoverObj), SeekOrigin.Current);
                            break;
                        }
                    case MAP_TERRAIN:
                        {
                            var mapObj = new C2DMapTerrainObj();
                            stream.Read(mapObj.FileName, 260);
                            stream.Read(&mapObj.PosCell, sizeof(MyPos));
                            mapObj.Load(_folder);

                            //The server only need the new LayerInfo, so it will be merged
                            //and the object will be deleted
                            foreach (var part in mapObj.Parts)
                            {
                                for (var j = 0; j < part.SizeBase.Width; j++)
                                {
                                    for (var k = 0; k < part.SizeBase.Height; k++)
                                    {
                                        var x = mapObj.PosCell.X + part.PosSceneOffset.X + j - part.SizeBase.Width;
                                        var y = mapObj.PosCell.Y + part.PosSceneOffset.Y + k - part.SizeBase.Height;
                                        _cells[x, y] = new Cell(part.Cells[j, k]);
                                    }
                                }
                            }
                            break;
                        }
                    case MAP_SOUND:
                        {
                            //Do nothing with it...
                            stream.Seek(sizeof(CMapSound), SeekOrigin.Current);
                            break;
                        }
                    case Map_3Deffect:
                        {
                            //Do nothing with it...
                            stream.Seek(sizeof(C3DMapEffect), SeekOrigin.Current);
                            break;
                        }
                    case Map_3Deffectnew:
                        {
                            //Do nothing with it...
                            stream.Seek(sizeof(C3DMapEffectNew), SeekOrigin.Current);
                            break;
                        }
                }
            }
        }
    }
}
