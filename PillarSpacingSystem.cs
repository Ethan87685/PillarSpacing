using System.Collections.Generic;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PillarSpacing
{
    /// <summary>
    /// 根据 1.6.2f1 的 Game.dll 实现：
    ///
    /// 普通道路/轨道/步道
    ///   建造工具按 NetGeometryData.m_ElevatedLength 把高架路段切成多段，桥墩放在切点（节点）上。
    ///   原版：道路和轨道 80m，部分步道 40m。路段最长不超过 m_EdgeLengthRange.max（原版 200m），
    ///   所以间距超过 200m 时要同时放宽路段最大长度。
    ///
    /// 预设桥梁（Bridge 组件，由固定段 FixedNetElement 拼成：斜拉桥、悬索桥、拱桥、大桥等）
    ///   建造工具先按总长度算出要几组“单元”，再把单元按比例拉伸到所拉的长度，
    ///   桥塔落在单元内的固定比例位置，没有可调的“间距”。所以这类预设一律不改。
    ///
    /// 只改上述数值，不重建子物体、截面等数据，避免影响码头、开合桥等其他预设。
    /// </summary>
    public partial class PillarSpacingSystem : GameSystemBase
    {
        // 设置变化或存档加载完成时置为 true，下一帧应用
        public static bool Dirty = true;

        private EntityQuery m_NetPrefabQuery;

        // 每个预设第一次被修改前的原版数据，用于恢复
        private struct Original
        {
            public float ElevatedLength;
            public Bounds1 EdgeLengthRange;
        }

        private readonly Dictionary<Entity, Original> m_Originals = new Dictionary<Entity, Original>();

        private PrefabSystem m_PrefabSystem;

        // 每次加载存档后，把桥梁和疑似码头类预设列到日志里一次，方便排查
        private bool m_PrefabListLogged;
        private static readonly System.Text.RegularExpressions.Regex kDockKeywords =
            new System.Text.RegularExpressions.Regex("dock|quay|pier|harbo|port|fish|oil|draw|lift|bascule|swing",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        protected override void OnCreate()
        {
            base.OnCreate();

            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            m_NetPrefabQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<PrefabData>(),
                    ComponentType.ReadWrite<NetGeometryData>(),
                },
                Any = new[]
                {
                    ComponentType.ReadOnly<RoadData>(),
                    ComponentType.ReadOnly<TrackData>(),
                    ComponentType.ReadOnly<PathwayData>(),
                    ComponentType.ReadWrite<FixedNetElement>(),
                },
            });
        }

        // 每次进入存档或编辑器后自动应用一次，解决“每次启动都要手动开关”的问题
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            Dirty = true;
            m_PrefabListLogged = false;
        }

        protected override void OnUpdate()
        {
            if (!Dirty || Mod.Setting == null)
                return;

            Dirty = false;
            Apply();
        }

        private enum NetCategory { Road, Track, Path, Skip }

        private NetCategory Classify(Entity e)
        {
            // 预设桥梁、水电站大坝等由固定段组成的网络：桥塔位置按比例分布，不是间距，不修改
            if (EntityManager.HasBuffer<FixedNetElement>(e))
                return NetCategory.Skip;
            if (EntityManager.HasComponent<RoadData>(e)) return NetCategory.Road;      // 含电车道路
            if (EntityManager.HasComponent<TrackData>(e)) return NetCategory.Track;
            return NetCategory.Path;
        }

        private void Apply()
        {
            Setting s = Mod.Setting;
            int roads = 0, tracks = 0, paths = 0, restored = 0, skipped = 0;

            using (NativeArray<Entity> entities = m_NetPrefabQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity e in entities)
                {
                    NetCategory category = Classify(e);
                    if (category == NetCategory.Skip)
                    {
                        skipped++;
                        continue;
                    }

                    Original orig = GetOrRecordOriginal(e);
                    float interval = category == NetCategory.Track ? s.TrackInterval
                                   : category == NetCategory.Path ? s.PathInterval
                                   : s.RoadInterval;

                    if (!ApplyNormal(e, orig, s.Enabled, interval))
                        continue;

                    if (!s.Enabled) restored++;
                    else if (category == NetCategory.Track) tracks++;
                    else if (category == NetCategory.Path) paths++;
                    else roads++;
                }
            }

            if (!m_PrefabListLogged)
            {
                m_PrefabListLogged = true;
                LogPrefabList();
            }

            Mod.log.Info($"Pillar spacing applied (road {s.RoadInterval}m, track {s.TrackInterval}m, path {s.PathInterval}m): " +
                         $"changed roads {roads}, tracks {tracks}, paths {paths}, restored {restored}, skipped bridge prefabs {skipped}");
        }

        // 列出所有预设桥梁（处理的和跳过的），以及名字像码头、开合桥的网络预设
        private void LogPrefabList()
        {
            var lines = new System.Text.StringBuilder();
            int count = 0;

            using (NativeArray<Entity> entities = m_NetPrefabQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity e in entities)
                {
                    string name = GetPrefabName(e);
                    NetCategory category = Classify(e);
                    bool isBridge = category == NetCategory.Skip;
                    if (!isBridge && !kDockKeywords.IsMatch(name))
                        continue;

                    NetGeometryData geo = EntityManager.GetComponentData<NetGeometryData>(e);
                    string placement = EntityManager.HasComponent<PlaceableNetData>(e)
                        ? EntityManager.GetComponentData<PlaceableNetData>(e).m_PlacementFlags.ToString()
                        : "-";
                    int fixedCount = EntityManager.HasBuffer<FixedNetElement>(e)
                        ? EntityManager.GetBuffer<FixedNetElement>(e, true).Length
                        : 0;

                    // 显示记录下来的原版值，而不是被本 Mod 改过的值
                    Original orig = m_Originals.TryGetValue(e, out Original o) ? o : default;
                    Bounds1 edge = m_Originals.ContainsKey(e) ? orig.EdgeLengthRange : geo.m_EdgeLengthRange;
                    float elevated = m_Originals.ContainsKey(e) ? orig.ElevatedLength : geo.m_ElevatedLength;

                    lines.AppendLine($"  [{category}] {name} | elevatedLength={elevated} edgeLength={edge.min}-{edge.max} " +
                                     $"fixedSegments={fixedCount} geometryFlags={geo.m_Flags} placementFlags={placement}");
                    count++;
                }
            }

            Mod.log.Info($"Bridge / dock related net prefabs ({count}):\n{lines}");
        }

        private string GetPrefabName(Entity e)
        {
            try
            {
                PrefabData data = EntityManager.GetComponentData<PrefabData>(e);
                return m_PrefabSystem.GetPrefab<PrefabBase>(data)?.name ?? "<unknown>";
            }
            catch
            {
                return "<unknown>";
            }
        }

        private Original GetOrRecordOriginal(Entity e)
        {
            if (m_Originals.TryGetValue(e, out Original orig))
                return orig;

            NetGeometryData geo = EntityManager.GetComponentData<NetGeometryData>(e);
            orig.ElevatedLength = geo.m_ElevatedLength;
            orig.EdgeLengthRange = geo.m_EdgeLengthRange;
            m_Originals[e] = orig;
            return orig;
        }

        // 普通道路、轨道、步道
        private bool ApplyNormal(Entity e, Original orig, bool modify, float interval)
        {
            // 原版间距不小于路段最大长度的预设（如机场滑行道、地面专用路）不会因间距产生桥墩，跳过
            if (orig.ElevatedLength <= 0f || orig.ElevatedLength >= orig.EdgeLengthRange.max - 0.01f)
                return false;

            float targetElevated = modify ? interval : orig.ElevatedLength;
            // 间距超过路段最大长度时同步放宽，否则会被每 200m 一个节点卡住
            float targetEdgeMax = modify ? math.max(orig.EdgeLengthRange.max, interval) : orig.EdgeLengthRange.max;

            NetGeometryData geo = EntityManager.GetComponentData<NetGeometryData>(e);
            if (geo.m_ElevatedLength == targetElevated && geo.m_EdgeLengthRange.max == targetEdgeMax)
                return false;

            geo.m_ElevatedLength = targetElevated;
            geo.m_EdgeLengthRange.max = targetEdgeMax;
            EntityManager.SetComponentData(e, geo);
            return true;
        }
    }
}
