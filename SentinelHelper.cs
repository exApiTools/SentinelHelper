using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ExileCore.Shared.Interfaces;
using GameOffsets;
using ImGuiNET;
using SharpDX;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace SentinelHelper;

public class SentinelHelper : BaseSettingsPlugin<SentinelHelperSettings>
{
    private readonly Dictionary<SentinelType, SentinelTypeRuntimeInfo> _typeRuntimeInfo =
        new Dictionary<SentinelType, SentinelTypeRuntimeInfo>
        {
            [SentinelType.Blue] = new SentinelTypeRuntimeInfo(),
            [SentinelType.Red] = new SentinelTypeRuntimeInfo(),
            [SentinelType.Yellow] = new SentinelTypeRuntimeInfo(),
        };

    private Dictionary<SentinelType, MonsterCount> _monsterCounts = GetMonsterCountDictionary();
    private DateTime _lastUseTime = DateTime.UtcNow;

    private static Dictionary<SentinelType, MonsterCount> GetMonsterCountDictionary()
    {
        return new Dictionary<SentinelType, MonsterCount>
        {
            [SentinelType.Blue] = new MonsterCount(),
            [SentinelType.Red] = new MonsterCount(),
            [SentinelType.Yellow] = new MonsterCount(),
        };
    }

    public override bool Initialise()
    {
        foreach (var sentinelType in Enum.GetValues<SentinelType>())
        {
            var list = new List<ISettingsHolder>();
            SettingsParser.Parse(Settings.PerTypeSettings[sentinelType], list);
            _typeRuntimeInfo[sentinelType].ParsedSettings = list;
        }

        CompilePredicates();
        return true;
    }

    public override void DrawSettings()
    {
        base.DrawSettings();
        ImGui.TextWrapped("You can use most arithmetic/logical operations in conditions. Available variables are Total, Normal, Magic, Rare, and Unique. Try e.g. \"Total>20\"");
        foreach (var (type, info) in _typeRuntimeInfo)
        {
            ImGui.Separator();
            ImGui.PushID(type.ToString());
            ImGui.Text(type.ToString());
            foreach (var holder in info.ParsedSettings)
            {
                holder.Draw();
            }

            if (info.Predicate?.LastException != null)
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), $"Unable to parse: {info.Predicate.LastException}");
            }

            ImGui.PopID();
        }

        CompilePredicates();
    }

    private void CompilePredicates()
    {
        foreach (var (type, info) in _typeRuntimeInfo)
        {
            var settingsValue = Settings.PerTypeSettings[type].Condition.Value;
            if (info.Predicate == null || info.Predicate.Source != settingsValue)
            {
                try
                {
                    var func = DynamicExpressionParser.ParseLambda<MonsterCount, bool>(null, false, settingsValue)
                        .Compile();
                    info.Predicate = new CompiledPredicate(settingsValue, func, null);
                }
                catch (Exception ex)
                {
                    info.Predicate = new CompiledPredicate(settingsValue, null, ex.Message ?? ex.ToString());
                }
            }
        }
    }

    public override Job Tick()
    {
        CompilePredicates();
        var containers = GetMonsterCountDictionary();
        foreach (var entity in GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster])
        {
            if (!entity.IsHostile || !entity.IsAlive)
            {
                continue;
            }

            if (entity.TryGetComponent<Buffs>(out var buffs) &&
                buffs.BuffsList.Any(x => x.Name.StartsWith("sentinel_tag_visual_")))
            {
                continue;
            }

            if (!entity.TryGetComponent<ObjectMagicProperties>(out var omp) ||
                omp.Rarity == MonsterRarity.Error)
            {
                continue;
            }

            if (Settings.IgnoreIdleMonsters &&
                (!entity.TryGetComponent<Actor>(out var actor) ||
                 actor.Animation == AnimationE.Idle))
            {
                continue;
            }

            foreach (var container in containers)
            {
                if (entity.DistancePlayer > Settings.PerTypeSettings[container.Key].NearbyMonsterRange.Value)
                {
                    continue;
                }

                switch (omp.Rarity)
                {
                    case MonsterRarity.White:
                        container.Value.Normal++;
                        break;
                    case MonsterRarity.Magic:
                        container.Value.Magic++;
                        break;
                    case MonsterRarity.Rare:
                        container.Value.Rare++;
                        break;
                    case MonsterRarity.Unique:
                        container.Value.Unique++;
                        break;
                }
            }

            _monsterCounts = containers;
        }

        return null;
    }

    public override void Render()
    {
        foreach (var sentinelType in Enum.GetValues<SentinelType>())
        {
            var buttonElement = sentinelType switch
            {
                SentinelType.Red => GameController.IngameState.IngameUi.GameUI?.SentinelPanel?.RedSentinelSubPanel,
                SentinelType.Blue => GameController.IngameState.IngameUi.GameUI?.SentinelPanel?.BlueSentinelSubPanel,
                SentinelType.Yellow => GameController.IngameState.IngameUi.GameUI?.SentinelPanel
                    ?.YellowSentinelSubPanel,
            };

            var shortcut = sentinelType switch
            {
                SentinelType.Red => GameController.IngameState.ShortcutSettings?.StalkerSentinelShortcut,
                SentinelType.Blue => GameController.IngameState.ShortcutSettings?.PandemoniumSentinelShortcut,
                SentinelType.Yellow => GameController.IngameState.ShortcutSettings?.ApexSentinelShortcut,
            };

            if (buttonElement is { IsVisible: true })
            {
                if (Settings.PerTypeSettings[sentinelType].DisplayMonsterCountInRange)
                {
                    ImGui.GetBackgroundDrawList()
                        .AddText(buttonElement.GetClientRectCache.TopLeft.ToVector2Num() -
                                 new Vector2(0, ImGui.GetTextLineHeight()),
                            Color.White.ToImgui(), _monsterCounts[sentinelType].Total.ToString());
                }

                if (shortcut != null)
                {
                    if (shortcut.Value.Modifier != ShortcutModifier.None)
                    {
                        ImGui.GetBackgroundDrawList().AddText(new Vector2(0, 0), Color.Red.ToImgui(),
                            "Please use a sentinel shortcut without modifiers");
                    }
                    else if (DateTime.UtcNow - _lastUseTime > TimeSpan.FromSeconds(1) &&
                             buttonElement.SentinelData.StateFlags == SentinelDataFlags.Usable &&
                             _typeRuntimeInfo[sentinelType].Predicate?.Function is { } func &&
                             func(_monsterCounts[sentinelType]))
                    {
                        var key = (Keys)shortcut.Value.MainKey;
                        _lastUseTime = DateTime.UtcNow;
                        Input.KeyDown(key);
                        Input.KeyUp(key);
                    }
                }
            }
        }
    }
}