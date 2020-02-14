using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using YiX.Entities;
using YiX.Enums;
using YiX.Helpers;
using YiX.Items;
using YiX.Network.Packets.Conquer;
using YiX.SelfContainedSystems;
using YiX.Structures;
using YiX.World.Pathfinding;

namespace YiX.World
{
    public class Map
    {
        public ushort Id;
        public ushort Height;
        public ushort Width;
        public float ExpModifier;
        public float DropModifier;
        public MapFlags Flags;
        public Tuple<ushort, ushort, ushort> RespawnLocation;
        public WeatherType Weather;
        public string Name { get; set; }
        public ushort MapDocId { get; set; }

        [JsonIgnore]
        public readonly ConcurrentDictionary<int, YiObj> Entities;
        public Dictionary<int, Portal> Portals;

        [JsonConstructor]
        public Map()
        {
            Id = 0;
            Height = 0;
            Width = 0;
            ExpModifier = 0;
            DropModifier = 0;
            Flags = 0;
            Weather = WeatherType.None;
            Portals = new Dictionary<int,Portal>();
            Entities = new ConcurrentDictionary<int, YiObj>();
        }


        public void LoadInEntity(YiObj obj)
        {
            Entities.AddOrUpdate(obj.UniqueId, obj);

            switch (obj)
            {
                case Npc npc:
                    AddNpc(obj);
                    break;
                case DynamicNpc npc:
                    AddNpc(obj);
                    break;
                case Monster monster:
                    AddMob(obj);
                    break;
                case FloorItem item:
                    AddItem(obj);
                    break;
            }
            
            ScreenSystem.Create(obj);
        }

        public void Enter(YiObj obj)
        {
            Entities.AddOrUpdate(obj.UniqueId, obj);

            switch (obj)
            {
                case Npc npc:
                    AddNpc(obj);
                    break;
                case DynamicNpc npc:
                    AddNpc(obj);
                    break;
                case Monster monster:
                    AddMob(obj);
                    break;
                case FloorItem item:
                    AddItem(obj);
                    break;
            }

            (obj as Player)?.Send(MsgAction.MapShowPacket(obj));
            (obj as Player)?.Send(MsgStatus.Create(Id, (uint) Flags));

            if (!ScreenSystem.Entities.ContainsKey(obj.UniqueId))
                ScreenSystem.Create(obj);

            ScreenSystem.Update(obj);

            if (!(obj is Player player))
                return;

            WeatherSystem.SetWeatherFor(player);
            DayNightSystem.SetTimeFor(player);
        }

        public void Leave(YiObj obj)
        {
            Entities.TryRemove(obj.UniqueId);

            switch (obj)
            {
                case Npc npc:
                    //RemoveNpc(obj);
                    break;
                case DynamicNpc npc:
                    //RemoveNpc(obj);
                    break;
                case Monster monster:
                    RemoveMob(obj);
                    break;
                case FloorItem item:
                    RemoveItem(obj);
                    break;
            }

            ScreenSystem.Send(obj, MsgAction.Create(obj, 0, MsgActionType.EntityRemove));
            ScreenSystem.ClearFor(obj);
        }

        public bool Find<T>(int uniqueId, out T obj)
        {
            obj = default(T);
            if (Entities.TryGetValue(uniqueId, out var found))
                obj = (T)(object)found;
            return obj != null;
        }

        public void Path(YiObj obj, int x = 409, int y = 355, int tox = 356, int toy = 356)
        {
            var args = new PathingMaster(this, new Position(obj.Location.X, obj.Location.Y), new Position(tox, toy));
            JpsAgent.FindPath(args);
        }
        public IEnumerable<Vector2> Path(Vector2 start, Vector2 end)
        {
            var args = new PathingMaster(this, start.ToPostion(),end.ToPostion());
            return JpsAgent.FindPath(args);
        }

        public int CountPlayers() => Entities.Values.OfType<Player>().Count();

        public int CountOnlinePlayers() => Entities.Values.OfType<Player>().Count(p => p.Online);

        public int CountMonsters() => Entities.Values.OfType<Monster>().Count();

        public int CountNpcs() => Entities.Values.OfType<Npc>().Count();

        public int CountDynNpcs() => Entities.Values.OfType<DynamicNpc>().Count();

        public int CountBots() => Entities.Values.OfType<Bot>().Count();

        public int CountFloorItems() => Entities.Values.OfType<FloorItem>().Count();

        public bool GroundValid(ushort x, ushort y)
        {
            if (x <= 0 || x >= Width - 1 || y <= 0 || y >= Height - 1) return false;
            if (!MapAccess.MapData.ContainsKey(MapDocId)) return true;
            return MapAccess.MapData[MapDocId].GroundLayer[(x + 1) * Width + y + 1];
        }

        public bool MobValid(ushort x, ushort y)
        {
            if (x <= 0 || x >= Width - 1 || y <= 0 || y >= Height - 1) return false;
            if (!MapAccess.MapData.ContainsKey(MapDocId)) return true;
            return MapAccess.MapData[MapDocId].MobLayer[(x + 1) * Width + y + 1];
        }

        public bool ItemValid(ushort x, ushort y)
        {
            if (x <= 0 || x >= Width - 1 || y <= 0 || y >= Height - 1)
                return false;
            if (!MapAccess.MapData.ContainsKey(MapDocId)) return true;
            return MapAccess.MapData[MapDocId].ItemLayer[(x + 1) * Width + y + 1];
        }

        private void AddNpc(YiObj obj)
        {
            if(!MapAccess.MapData.ContainsKey(MapDocId))return;
            if (obj.Location.X <= 0 || obj.Location.X >= Width - 1 || obj.Location.Y <= 0 || obj.Location.Y >= Height - 1)
                return;
            MapAccess.MapData[MapDocId].GroundLayer[(obj.Location.X + 1) * Width + obj.Location.Y + 1] = false;
        }

        public void AddMob(YiObj obj)
        {
            if (!MapAccess.MapData.ContainsKey(MapDocId)) return;
            if (obj.Location.X <= 0 || obj.Location.X >= Width - 1 || obj.Location.Y <= 0 || obj.Location.Y >= Height - 1)
                return;
            MapAccess.MapData[MapDocId].MobLayer[(obj.Location.X + 1) * Width + obj.Location.Y + 1] = false;
        }

        private void AddItem(YiObj obj)
        {
            if (!MapAccess.MapData.ContainsKey(MapDocId)) return;
            if (obj.Location.X <= 0 || obj.Location.X >= Width - 1 || obj.Location.Y <= 0 || obj.Location.Y >= Height - 1)
                return;
            MapAccess.MapData[MapDocId].ItemLayer[(obj.Location.X + 1) * Width + obj.Location.Y + 1] = false;
        }

        public void RemoveMob(YiObj obj)
        {
            if (!MapAccess.MapData.ContainsKey(MapDocId)) return;
            if (obj.Location.X <= 0 || obj.Location.X >= Width - 1 || obj.Location.Y <= 0 || obj.Location.Y >= Height - 1)
                return;
            MapAccess.MapData[MapDocId].MobLayer[(obj.Location.X + 1) * Width + obj.Location.Y + 1] = true;
        }

        public void MoveMob(Vector2 from, Vector2 to)
        {
            if (!MapAccess.MapData.ContainsKey(MapDocId)) return;
            MapAccess.MapData[MapDocId].MobLayer[(from.X + 1)*Width + from.Y + 1] = true;
            MapAccess.MapData[MapDocId].MobLayer[(to.X + 1)*Width + to.Y + 1] = false;
        }

        private void RemoveItem(YiObj obj)
        {
            if (!MapAccess.MapData.ContainsKey(MapDocId)) return;
            if (obj.Location.X <= 0 || obj.Location.X >= Width - 1 || obj.Location.Y <= 0 || obj.Location.Y >= Height - 1)
                return;
            MapAccess.MapData[MapDocId].ItemLayer[(obj.Location.X + 1) * Width + obj.Location.Y + 1] = true;
        }
    }
}