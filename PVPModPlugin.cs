using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using R2API;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using RiskOfOptions;
using RiskOfOptions.Options;
using RoR2;
using RoR2.ContentManagement;
using RoR2.ExpansionManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using static MonoMod.InlineRT.MonoModRule;

[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: HG.Reflection.SearchableAttribute.OptIn]
[assembly: HG.Reflection.SearchableAttribute.OptInAttribute]
[module: UnverifiableCode]
#pragma warning disable CS0618
#pragma warning restore CS0618
namespace PVPMod
{
    [BepInPlugin(ModGuid, ModName, ModVer)]
    [BepInDependency(R2API.R2API.PluginGUID, R2API.R2API.PluginVersion)]
    [BepInDependency(TeamsAPI.PluginGUID, TeamsAPI.PluginVersion)]
    [BepInDependency(NetworkingAPI.PluginGUID, NetworkingAPI.PluginVersion)]
    [BepInDependency(ModCompatabilities.RiskOfOptionsCompatAbility.ModGUID, BepInDependency.DependencyFlags.SoftDependency)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    [System.Serializable]
    public class PVPModPlugin : BaseUnityPlugin
    {
        public const string ModGuid = "com.brynzananas.pvpmod";
        public const string ModName = "PVP mod";
        public const string ModVer = "1.1.0";
        public static bool riskOfOptionsEnabled { get; private set; }
        public static PVPModPlugin instance { get; private set; }
        public static BepInEx.PluginInfo PInfo { get; private set; }
        public static ConfigFile configFile { get; private set; }
        public static List<TeamIndex> playerTeamIndeces = [];
        public static List<TeamDef> playerTeamDefs = [];
        public static List<CharacterBody> activePVPBodies = [];
        private static int _extraPlayerTeamsCount;
        public static AssetBundle assetBundle { get; private set; }
        public static PVPModItemDef StrongerDeathMark { get; private set; }
        public static PVPModItemDef IncreaseDamageByShieldAndReduceShieldRechargeTime { get; private set; }
        public static BuffDef StrongerDeathMarkBuff { get; private set; }
        public static List<ConfigEntryBase> configs = [];
        public static ConfigEntry<bool> EnablePVP;
        public static ConfigEntry<bool> EnableContent;
        public static ConfigEntry<float> PVPCountdownTimer;
        public static ConfigEntry<int> PVPItemRewardAmount;
        public static ConfigEntry<float> PVPItemRewardTier1Weight;
        public static ConfigEntry<float> PVPItemRewardTier2Weight;
        public static ConfigEntry<float> PVPItemRewardTier3Weight;
        public static ConfigEntry<float> PVPItemRewardBossWeight;
        public static ConfigEntry<float> PVPItemRewardEquipmentWeight;
        public static ConfigEntry<float> PVPItemRewardLunarItemWeight;
        public static ConfigEntry<float> PVPItemRewardLunarEquipmentWeight;
        public static ConfigEntry<float> PVPItemRewardLunarCombinedWeight;
        public static ConfigEntry<float> PVPItemRewardTier1VoidWeight;
        public static ConfigEntry<float> PVPItemRewardTier2VoidWeight;
        public static ConfigEntry<float> PVPItemRewardTier3VoidWeight;
        public static ConfigEntry<float> PVPItemRewardBossVoidWeight;
        public static ConfigEntry<int> PVPLoserLoseItemsAmount;
        public static ConfigEntry<ItemTier> PVPLoserLoseItemsRarity;
        public static ConfigEntry<int> PVPLoserSpinelAfflictionsAmount;
        public static ConfigEntry<int> StrongerDeathMarkMinimumDebuffsToTrigger;
        public static ConfigEntry<bool> StrongerDeathMarkCountDebuffStacks;
        public static ConfigEntry<float> StrongerDeathMarkDebuffDuration;
        public static ConfigEntry<float> StrongerDeathMarkDebuffDurationPerStack;
        public static ConfigEntry<float> StrongerDeathMarkDebuffDamageIncrease;
        public static ConfigEntry<float> StrongerDeathMarkDebuffDamageIncreasePerStack;
        public static ConfigEntry<float> IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldToDamageMultiplier;
        public static ConfigEntry<float> IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldToDamageMultiplierPerStack;
        public static ConfigEntry<float> IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldRegenSpeedIncrease;
        public static ConfigEntry<float> IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldRegenSpeedIncreasePerStack;
        public static BasicPickupDropTable PVPItemReward;
        public static ExpansionDef DLC1Expansion;
        public static int extraPlayerTeamsCount
        {
            get => _extraPlayerTeamsCount;
            set
            {
                if (_extraPlayerTeamsCount == value) return;
                _extraPlayerTeamsCount = value;
                Init();
            }
        }
        public static bool pvpEnabled;
        public void Awake()
        {
            instance = this;
            PInfo = Info;
            configFile = Config;
            riskOfOptionsEnabled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(ModCompatabilities.RiskOfOptionsCompatAbility.ModGUID);
            extraPlayerTeamsCount = 16;
            SetHooks();
            SetConfigs();
            DLC1Expansion = Addressables.LoadAssetAsync<ExpansionDef>("RoR2/DLC1/Common/DLC1.asset").WaitForCompletion();
            assetBundle = AssetBundle.LoadFromFileAsync(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(PInfo.Location), "assetbundles", "pvpmod")).assetBundle;
            foreach (Material material in assetBundle.LoadAllAssets<Material>())
            {
                if (!material.shader.name.StartsWith("StubbedRoR2")) continue;
                string shaderName = material.shader.name.Replace("StubbedRoR2", "RoR2") + ".shader";
                Shader replacementShader = Addressables.LoadAssetAsync<Shader>(shaderName).WaitForCompletion();
                if (replacementShader) material.shader = replacementShader;
            }
            PVPItemReward = assetBundle.LoadAsset<BasicPickupDropTable>("Assets/PVPmod/pdPVPItemReward.asset");
            PVPCountdownPrefab = assetBundle.LoadAsset<GameObject>("Assets/PVPmod/PVPCountdown.prefab");
            SetWeights();
            StrongerDeathMark = assetBundle.LoadAsset<PVPModItemDef>("Assets/PVPmod/StrongerDeathMark.asset").RegisterItemDef(StrongerDeathMarkEvents);
            IncreaseDamageByShieldAndReduceShieldRechargeTime = assetBundle.LoadAsset<PVPModItemDef>("Assets/PVPmod/IncreaseDamageByShieldAndReduceShieldRechargeTime.asset").RegisterItemDef(IncreaseDamageByShieldAndReduceShieldRechargeTimeEvents);
            if (EnableContent.Value)
                ContentManager.collectContentPackProviders += (addContentPackProvider) => addContentPackProvider(new PVPModContentPack());
            NetworkingAPI.RegisterMessageType<SpawnPVPCountdown>();
        }
        public static void InitLanguageTokens()
        {
            Utils.AddLanguageToken(StrongerDeathMark.nameToken, "Chaotic Death Mark of Doom");
            Utils.AddLanguageToken(StrongerDeathMark.pickupToken, $"Enemies with {Utils.damagePrefix}{StrongerDeathMarkMinimumDebuffsToTrigger.Value}{Utils.endPrefix} or more {(StrongerDeathMarkCountDebuffStacks.Value ? "debuff stacks" : "debuffs")} are marked for death, taking bonus damage.");
            Utils.AddLanguageToken(StrongerDeathMark.descriptionToken, $"Enemies with {Utils.damagePrefix}{StrongerDeathMarkMinimumDebuffsToTrigger.Value}{Utils.endPrefix}, or more {(StrongerDeathMarkCountDebuffStacks.Value ? "debuff stacks" : "debuffs")} are {Utils.damagePrefix}marked for death{Utils.endPrefix}, increasing damage taken by {Utils.damagePrefix}{StrongerDeathMarkDebuffDamageIncrease.Value}%{Utils.endPrefix} {Utils.stackPrefix}(+{StrongerDeathMarkDebuffDamageIncreasePerStack.Value}% per stack){Utils.endPrefix} from all sources for {Utils.utilityPrefix}{StrongerDeathMarkDebuffDuration.Value}{Utils.endPrefix} {Utils.stackPrefix} (+{StrongerDeathMarkDebuffDuration.Value} per stack){Utils.endPrefix} seconds.");
            Utils.AddLanguageToken(IncreaseDamageByShieldAndReduceShieldRechargeTime.nameToken, "Exoskeleton Of Ruinous Powers");
            Utils.AddLanguageToken(IncreaseDamageByShieldAndReduceShieldRechargeTime.pickupToken, "Increase damage by max shield and reduce shield recharge start time.");
            Utils.AddLanguageToken(IncreaseDamageByShieldAndReduceShieldRechargeTime.descriptionToken, $"Deal {Utils.healingPrefix}{IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldToDamageMultiplier.Value}%{Utils.endPrefix} {Utils.stackPrefix}(+{IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldToDamageMultiplierPerStack.Value}% per stack){Utils.endPrefix} of your max shields as {Utils.damagePrefix}damage{Utils.endPrefix}. Reduce shield recharge start time by {Utils.utilityPrefix}{IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldRegenSpeedIncrease.Value}%{Utils.endPrefix} {Utils.stackPrefix}(+{IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldRegenSpeedIncreasePerStack.Value}% per stack){Utils.endPrefix}.");
        }
        public static void SetConfigs()
        {
            EnablePVP = Utils.CreateConfig("Main", "Enable PVP", true, "Enable Players Versus Players after teleporter charge?");
            EnableContent = Utils.CreateConfig("Main", "Enable content", true, "Enable all mod content?");
            PVPCountdownTimer = Utils.CreateConfig("PVP", "Countdown timer", 3f, "Control countdown timer that begins PVP");
            PVPItemRewardAmount = Utils.CreateConfig("PVP", "Item reward amount", 1, "Control how much items will be spawned on pvp win. Items spawn on the teleporter");
            PVPItemRewardTier1Weight = Utils.CreateConfig("PVP", "Item reward common rarity weight", 100f, "Control weight for common rarity on random item reward selection");
            PVPItemRewardTier2Weight = Utils.CreateConfig("PVP", "Item reward uncommon rarity weight", 0f, "Control weight for uncommon rarity on random item reward selection");
            PVPItemRewardTier3Weight = Utils.CreateConfig("PVP", "Item reward legendary rarity weight", 0f, "Control weight for legendary rarity on random item reward selection");
            PVPItemRewardBossWeight = Utils.CreateConfig("PVP", "Item reward boss rarity weight", 0f, "Control weight for boss rarity on random item reward selection");
            PVPItemRewardEquipmentWeight = Utils.CreateConfig("PVP", "Item reward equipment rarity weight", 0f, "Control weight for equipment rarity on random item reward selection");
            PVPItemRewardLunarItemWeight = Utils.CreateConfig("PVP", "Item reward lunar item rarity weight", 0f, "Control weight for lunar item rarity on random item reward selection");
            PVPItemRewardLunarEquipmentWeight = Utils.CreateConfig("PVP", "Item reward lunar equipment rarity weight", 0f, "Control weight for lunar equipment rarity on random item reward selection");
            PVPItemRewardLunarCombinedWeight = Utils.CreateConfig("PVP", "Item reward lunar combined rarity weight", 0f, "Control weight for lunar combined rarity on random item reward selection");
            PVPItemRewardTier1VoidWeight = Utils.CreateConfig("PVP", "Item reward void common rarity weight", 0f, "Control weight for void common rarity on random item reward selection");
            PVPItemRewardTier2VoidWeight = Utils.CreateConfig("PVP", "Item reward void uncommon rarity weight", 0f, "Control weight for void uncommon rarity on random item reward selection");
            PVPItemRewardTier3VoidWeight = Utils.CreateConfig("PVP", "Item reward void legendary rarity weight", 0f, "Control weight for void legendary rarity on random item reward selection");
            PVPItemRewardBossVoidWeight = Utils.CreateConfig("PVP", "Item reward void boss rarity weight", 0f, "Control weight for void boss rarity on random item reward selection");
            PVPLoserLoseItemsAmount = Utils.CreateConfig("PVP", "Lose items amount", 1, "Control how much items PVP losers will lose?");
            PVPLoserLoseItemsRarity = Utils.CreateConfig("PVP", "Lose items rarity", ItemTier.Tier1, "Control which rarity of an item would be removed on PVP lose?");
            PVPLoserSpinelAfflictionsAmount = Utils.CreateConfig("PVP", "Gain Tonic Affliction", 1, "Control how much Tonic Afflictions PVP losers will get?");
            PVPItemRewardTier1Weight.SettingChanged += PVPItemRewardTier1Weight_SettingChanged;
            PVPItemRewardTier2Weight.SettingChanged += PVPItemRewardTier1Weight_SettingChanged;
            PVPItemRewardTier3Weight.SettingChanged += PVPItemRewardTier1Weight_SettingChanged;
            PVPItemRewardBossWeight.SettingChanged += PVPItemRewardTier1Weight_SettingChanged;
            PVPItemRewardEquipmentWeight.SettingChanged += PVPItemRewardTier1Weight_SettingChanged;
            PVPItemRewardLunarItemWeight.SettingChanged += PVPItemRewardTier1Weight_SettingChanged;
            PVPItemRewardLunarEquipmentWeight.SettingChanged += PVPItemRewardTier1Weight_SettingChanged;
            PVPItemRewardLunarCombinedWeight.SettingChanged += PVPItemRewardTier1Weight_SettingChanged;
            PVPItemRewardTier1VoidWeight.SettingChanged += PVPItemRewardTier1Weight_SettingChanged;
            PVPItemRewardTier2VoidWeight.SettingChanged += PVPItemRewardTier1Weight_SettingChanged;
            PVPItemRewardTier3VoidWeight.SettingChanged += PVPItemRewardTier1Weight_SettingChanged;
            PVPItemRewardBossVoidWeight.SettingChanged += PVPItemRewardTier1Weight_SettingChanged;
        }
        private static void PVPItemRewardTier1Weight_SettingChanged(object sender, EventArgs e) => SetWeights();
        public void OnDestroy()
        {
            UnsetHooks();
        }
        private static bool hooksSet;
        private static void SetHooks()
        {
            if (hooksSet) return;
            hooksSet = true;
            TeleporterInteraction.onTeleporterChargedGlobal += TeleporterInteraction_onTeleporterChargedGlobal;
            Stage.onServerStageBegin += Stage_onServerStageBegin;
            TeamComponent.onJoinTeamGlobal += TeamComponent_onJoinTeamGlobal;
            TeamComponent.onLeaveTeamGlobal += TeamComponent_onLeaveTeamGlobal;
            GlobalEventManager.onCharacterDeathGlobal += GlobalEventManager_onCharacterDeathGlobal;
            On.RoR2.TeamManager.SetTeamExperience += TeamManager_SetTeamExperience;
            RoR2Application.onLoadFinished += InitLanguageTokens;
        }
        private static bool thisIsStupid;
        private static void TeamManager_SetTeamExperience(On.RoR2.TeamManager.orig_SetTeamExperience orig, TeamManager self, TeamIndex teamIndex, ulong newExperience)
        {
            orig(self, teamIndex, newExperience);
            if (thisIsStupid || !pvpEnabled || !playerTeamIndeces.Contains(teamIndex)) return;
            thisIsStupid = true;
            self.SetTeamExperience(TeamIndex.Player, newExperience);
            foreach (TeamIndex teamIndex1 in playerTeamIndeces)
            {
                if (teamIndex1 == teamIndex) continue;
                self.SetTeamExperience(teamIndex1, newExperience);
            }
            thisIsStupid = false;
        }

        public static void SetWeights()
        {
            bool ifIDontDoThisIMightBeKilledByGearbox = Run.instance ? Run.instance.IsExpansionEnabled(DLC1Expansion) : false;
            PVPItemReward.tier1Weight = PVPItemRewardTier1Weight.Value;
            PVPItemReward.tier2Weight = PVPItemRewardTier2Weight.Value;
            PVPItemReward.tier3Weight = PVPItemRewardTier3Weight.Value;
            PVPItemReward.bossWeight = PVPItemRewardBossWeight.Value;
            PVPItemReward.equipmentWeight = PVPItemRewardEquipmentWeight.Value;
            PVPItemReward.lunarItemWeight = PVPItemRewardLunarItemWeight.Value;
            PVPItemReward.lunarEquipmentWeight = PVPItemRewardLunarEquipmentWeight.Value;
            PVPItemReward.lunarCombinedWeight = PVPItemRewardLunarCombinedWeight.Value;
            PVPItemReward.voidTier1Weight = ifIDontDoThisIMightBeKilledByGearbox ? PVPItemRewardTier1VoidWeight.Value : 0f;
            PVPItemReward.voidTier2Weight = ifIDontDoThisIMightBeKilledByGearbox ? PVPItemRewardTier2VoidWeight.Value : 0f;
            PVPItemReward.voidTier3Weight = ifIDontDoThisIMightBeKilledByGearbox ? PVPItemRewardTier3VoidWeight.Value : 0f;
            PVPItemReward.voidBossWeight = ifIDontDoThisIMightBeKilledByGearbox ? PVPItemRewardBossVoidWeight.Value : 0f;
        }
        private static void TeamComponent_onLeaveTeamGlobal(TeamComponent arg1, TeamIndex arg2)
        {
            CharacterBody characterBody = arg1.body;
            if (characterBody == null || !characterBody.isPlayerControlled || !playerTeamIndeces.Contains(arg2)) return;
            if (!activePVPBodies.Contains(characterBody)) return;
            activePVPBodies.Remove(characterBody);
        }

        private static void TeamComponent_onJoinTeamGlobal(TeamComponent arg1, TeamIndex arg2)
        {
            CharacterBody characterBody = arg1.body;
            if (characterBody == null || !characterBody.isPlayerControlled || !playerTeamIndeces.Contains(arg2)) return;
            if (activePVPBodies.Contains(characterBody)) return;
            activePVPBodies.Add(characterBody);
        }

        private static void GlobalEventManager_onCharacterDeathGlobal(DamageReport obj)
        {
            if (!pvpEnabled || !NetworkServer.active) return;
            TeamIndex teamIndex = obj.victimTeamIndex;
            if (!playerTeamIndeces.Contains(teamIndex)) return;
            if (obj.victimMaster && obj.victimMaster.playerCharacterMasterController && !losers.Contains(obj.victimMaster.playerCharacterMasterController)) losers.Add(obj.victimMaster.playerCharacterMasterController);
            int alive = 0;
            CharacterBody lastPlayer = null;
            foreach (CharacterBody characterBody in activePVPBodies)
            {
                if (characterBody == null || !characterBody.isPlayerControlled) continue;
                bool isDead = characterBody.master ? characterBody.master.IsDeadAndOutOfLivesServer() : true;
                if (isDead) continue;
                lastPlayer = characterBody;
                alive++;
            }
            if (alive > 1) return;
            EndPVP(true);
            SpawnItems();
        }
        public static void SpawnItems()
        {
            if (Run.instance == null || Run.instance.bossRewardRng == null) return;
            if (TeleporterInteraction.instance == null || TeleporterInteraction.instance.bossGroup == null) return;
            if (PVPItemRewardAmount.Value <= 0) return;
            float num2 = 360f / PVPItemRewardAmount.Value;
            Vector3 vector = Quaternion.AngleAxis(UnityEngine.Random.Range(0, 360), Vector3.up) * (Vector3.up * 40f + Vector3.forward * 5f);
            Quaternion quaternion = Quaternion.AngleAxis(num2, Vector3.up);
            for (int i = 0; i < PVPItemRewardAmount.Value; i++)
            {
                PickupIndex pickupIndex = PVPItemReward.GenerateDrop(Run.instance.bossRewardRng);
                PickupDropletController.CreatePickupDroplet(pickupIndex, TeleporterInteraction.instance.bossGroup.dropPosition.position, vector);
                vector = quaternion * vector;
            }
        }

        private static void StrongerDeathMarkEvents(ItemDef itemDef)
        {
            StrongerDeathMarkBuff = assetBundle.LoadAsset<BuffDef>("Assets/PVPmod/bdStrongerDeathMark.asset").RegisterBuffDef(StrongerDeathMarkBuffEvents);
            StrongerDeathMarkMinimumDebuffsToTrigger = Utils.CreateConfig(itemDef.name, "Minimum debuffs", 15, "Minimum debuffs needed for adding damage increase debuff on debuff addition");
            StrongerDeathMarkCountDebuffStacks = Utils.CreateConfig(itemDef.name, "Count stacks", true, "Count debuff stacks for effect triggering");
            StrongerDeathMarkDebuffDuration = Utils.CreateConfig(itemDef.name, "Debuff duration", 10f, "Control debuff duration in seconds");
            StrongerDeathMarkDebuffDurationPerStack = Utils.CreateConfig(itemDef.name, "Debuff duration per stack", 5f, "Control debuff duration per stack in seconds");
            StrongerDeathMarkDebuffDamageIncrease = Utils.CreateConfig(itemDef.name, "Debuff damage multiplier", 600f, "Control debuff taken damage multiplier in percentage");
            StrongerDeathMarkDebuffDamageIncreasePerStack = Utils.CreateConfig(itemDef.name, "Debuff damage multiplier per stack", 300f, "Control debuff taken damage multiplier per stack in percentage");
            StrongerDeathMarkMinimumDebuffsToTrigger.SettingChanged += StrongerDeathMarkMinimumDebuffsToTrigger_SettingChanged;
            StrongerDeathMarkCountDebuffStacks.SettingChanged += StrongerDeathMarkMinimumDebuffsToTrigger_SettingChanged;
            StrongerDeathMarkDebuffDuration.SettingChanged += StrongerDeathMarkMinimumDebuffsToTrigger_SettingChanged;
            StrongerDeathMarkDebuffDurationPerStack.SettingChanged += StrongerDeathMarkMinimumDebuffsToTrigger_SettingChanged;
            StrongerDeathMarkDebuffDamageIncrease.SettingChanged += StrongerDeathMarkMinimumDebuffsToTrigger_SettingChanged;
            StrongerDeathMarkDebuffDamageIncreasePerStack.SettingChanged += StrongerDeathMarkMinimumDebuffsToTrigger_SettingChanged;
            void StrongerDeathMarkBuffEvents(BuffDef buffDef)
            {
                On.RoR2.CharacterBody.HandleCascadingBuffs += CharacterBody_HandleCascadingBuffs;
                void CharacterBody_HandleCascadingBuffs(On.RoR2.CharacterBody.orig_HandleCascadingBuffs orig, CharacterBody self)
                {
                    orig(self);
                    int itemCount = Util.GetItemCountGlobal(itemDef.itemIndex, true, true) - (self.teamComponent ? Util.GetItemCountForTeam(self.teamComponent.teamIndex, itemDef.itemIndex, true, true) : (self.inventory ? self.inventory.GetItemCount(itemDef) : 0));
                    if (itemCount <= 0) return;
                    if (self.HasBuff(buffDef)) return;
                    int num = 0;
                    foreach (BuffIndex buffIndex in BuffCatalog.debuffBuffIndices) if (self.HasBuff(buffIndex)) num += StrongerDeathMarkCountDebuffStacks.Value ? self.GetBuffCount(buffIndex) : 1;
                    if (num >= StrongerDeathMarkMinimumDebuffsToTrigger.Value) self.AddTimedBuff(buffDef, Utils.GetStackingFloat(StrongerDeathMarkDebuffDuration.Value, StrongerDeathMarkDebuffDurationPerStack.Value, itemCount));
                }
                IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
                void HealthComponent_TakeDamageProcess(MonoMod.Cil.ILContext il)
                {
                    ILCursor c = new ILCursor(il);
                    ILLabel iLLabel = null;
                    int locId = 0;
                    if (c.TryGotoNext(MoveType.After,
                            x => x.MatchLdarg(0),
                            x => x.MatchLdfld<HealthComponent>(nameof(HealthComponent.body)),
                            x => x.MatchLdsfld(typeof(RoR2Content.Buffs), nameof(RoR2Content.Buffs.DeathMark)),
                            x => x.MatchCallvirt<CharacterBody>(nameof(CharacterBody.HasBuff)),
                            x => x.MatchBrfalse(out iLLabel),
                            x => x.MatchLdloc(out locId)
                        ))
                    {
                        c.GotoLabel(iLLabel, MoveType.Before);
                        Instruction instruction = c.Emit(OpCodes.Ldarg_0).Prev;
                        iLLabel.Target = instruction;
                        c.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(HealthComponent), nameof(HealthComponent.body)));
                        c.Emit(OpCodes.Ldarg_1);
                        c.EmitDelegate(HandleStrongerDeathMark);
                        float HandleStrongerDeathMark(CharacterBody characterBody, DamageInfo damageInfo)
                        {
                            if (!characterBody.HasBuff(buffDef) || damageInfo.attacker == null) return 1f;
                            CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                            if (attackerBody == null) return StrongerDeathMarkDebuffDamageIncrease.Value / 100f + 1f;
                            Inventory inventory = attackerBody.inventory;
                            if (inventory == null) return StrongerDeathMarkDebuffDamageIncrease.Value / 100f + 1f;
                            int itemCount = inventory.GetItemCount(itemDef);
                            if (itemCount <= 0) return StrongerDeathMarkDebuffDamageIncrease.Value / 100f + 1f;
                            return Utils.GetStackingFloat(StrongerDeathMarkDebuffDamageIncrease.Value, StrongerDeathMarkDebuffDamageIncreasePerStack.Value, itemCount) / 100f + 1f;
                        }
                        c.Emit(OpCodes.Ldloc, locId);
                        c.Emit(OpCodes.Mul);
                        c.Emit(OpCodes.Stloc, locId);
                    }
                    else
                    {
                        instance.Logger.LogError(il.Method.Name + " IL Hook failed!");
                    }
                }
            }
            
        }

        private static void StrongerDeathMarkMinimumDebuffsToTrigger_SettingChanged(object sender, EventArgs e) => InitLanguageTokens();

        private static void IncreaseDamageByShieldAndReduceShieldRechargeTimeEvents(ItemDef itemDef)
        {
            IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldToDamageMultiplier = Utils.CreateConfig(itemDef.name, "Max shield to damage multiplier", 5f, "Control max shield to damage multiplier in percentage");
            IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldToDamageMultiplierPerStack = Utils.CreateConfig(itemDef.name, "Max shield to damage multiplier per stack", 2.5f, "Control max shield to damage multiplier per stack in percentage");
            IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldRegenSpeedIncrease = Utils.CreateConfig(itemDef.name, "Shield regen cooldown reduction", 30f, "Control shield regeneration cooldown reduction in percentage");
            IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldRegenSpeedIncreasePerStack = Utils.CreateConfig(itemDef.name, "Shield regen cooldown reduction per stack", 15f, "Control shield regeneration cooldown reduction in percentage");
            IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldToDamageMultiplier.SettingChanged += StrongerDeathMarkMinimumDebuffsToTrigger_SettingChanged;
            IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldToDamageMultiplierPerStack.SettingChanged += StrongerDeathMarkMinimumDebuffsToTrigger_SettingChanged;
            IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldRegenSpeedIncrease.SettingChanged += StrongerDeathMarkMinimumDebuffsToTrigger_SettingChanged;
            IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldRegenSpeedIncreasePerStack.SettingChanged += StrongerDeathMarkMinimumDebuffsToTrigger_SettingChanged;
            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
            void HealthComponent_TakeDamageProcess(MonoMod.Cil.ILContext il)
            {
                ILCursor c = new ILCursor(il);
                ILLabel iLLabel = null;
                int locId = 0;
                if (c.TryGotoNext(MoveType.After,
                        x => x.MatchLdarg(0),
                        x => x.MatchLdfld<HealthComponent>(nameof(HealthComponent.body)),
                        x => x.MatchLdsfld(typeof(RoR2Content.Buffs), nameof(RoR2Content.Buffs.DeathMark)),
                        x => x.MatchCallvirt<CharacterBody>(nameof(CharacterBody.HasBuff)),
                        x => x.MatchBrfalse(out iLLabel),
                        x => x.MatchLdloc(out locId)
                    ))
                {
                    c.GotoLabel(iLLabel, MoveType.Before);
                    Instruction instruction = c.Emit(OpCodes.Ldarg_0).Prev;
                    iLLabel.Target = instruction;
                    c.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(HealthComponent), nameof(HealthComponent.body)));
                    c.Emit(OpCodes.Ldarg_1);
                    c.EmitDelegate(HandleIncreaseDamageByShieldAndReduceShieldRechargeTime);
                    float HandleIncreaseDamageByShieldAndReduceShieldRechargeTime(CharacterBody characterBody, DamageInfo damageInfo)
                    {
                        if (damageInfo.attacker == null) return 1f;
                        CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                        if (attackerBody == null) return 1f;
                        Inventory inventory = attackerBody.inventory;
                        if (inventory == null) return attackerBody.maxShield * IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldToDamageMultiplier.Value / 100f + 1f;
                        int itemCount = inventory.GetItemCount(itemDef);
                        if (itemCount <= 0) return attackerBody.maxShield * IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldToDamageMultiplier.Value / 100f + 1f;
                        return attackerBody.maxShield * Utils.GetStackingFloat(IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldToDamageMultiplier.Value, IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldToDamageMultiplierPerStack.Value, itemCount) / 100f + 1f;
                    }
                    c.Emit(OpCodes.Ldloc, locId);
                    c.Emit(OpCodes.Add);
                    c.Emit(OpCodes.Stloc, locId);
                }
                else
                {
                    instance.Logger.LogError(il.Method.Name + " IL Hook failed!");
                }
            }
            On.RoR2.CharacterBody.OnInventoryChanged += CharacterBody_OnInventoryChanged;
            void CharacterBody_OnInventoryChanged(On.RoR2.CharacterBody.orig_OnInventoryChanged orig, CharacterBody self)
            {
                orig(self);
                if (NetworkServer.active)
                    self.AddItemBehavior<IncreaseDamageByShieldAndReduceShieldRechargeTimeBehaviour>(self.inventory ? self.inventory.GetItemCount(itemDef) : 0);
            }
        }

        private static void UnsetHooks()
        {
            if (!hooksSet) return;
            hooksSet = false;
            TeleporterInteraction.onTeleporterChargedGlobal -= TeleporterInteraction_onTeleporterChargedGlobal;
            Stage.onServerStageBegin -= Stage_onServerStageBegin;
            TeamComponent.onJoinTeamGlobal -= TeamComponent_onJoinTeamGlobal;
            TeamComponent.onLeaveTeamGlobal -= TeamComponent_onLeaveTeamGlobal;
            GlobalEventManager.onCharacterDeathGlobal -= GlobalEventManager_onCharacterDeathGlobal;
            On.RoR2.TeamManager.SetTeamExperience -= TeamManager_SetTeamExperience;
            RoR2Application.onLoadFinished -= InitLanguageTokens;
        }
        private static void Stage_onServerStageBegin(Stage obj) => EndPVP(false);
        private static void TeleporterInteraction_onTeleporterChargedGlobal(TeleporterInteraction obj) => StartPVP();
        public static bool startingPVP = false;
        public static float startingStopwatch;
        public static GameObject PVPCountdownPrefab;
        public static List<PlayerCharacterMasterController> losers = [];
        public static void StartPVP()
        {
            if (!EnablePVP.Value || !NetworkServer.active) return;
            calling = false;
            int playersCount = GetAlivePlayers().Count;
            if (playersCount <= 1) return;
            startingPVP = true;
            startingStopwatch = PVPCountdownTimer.Value;
            new SpawnPVPCountdown(startingStopwatch).Send(NetworkDestination.Clients);
        }
        public static void BeginPVP()
        {
            SetWeights();
            if (!NetworkServer.active || pvpEnabled || !EnablePVP.Value) return;
            calling = false;
            int playersCount = GetAlivePlayers().Count;
            if (playersCount <= 1) return;
            playersCount = PlayerCharacterMasterController.instances.Count;
            pvpEnabled = true;
            losers.Clear();
            networkUsers.Clear();
            int readyTeamsCount = playerTeamDefs.Count;
            int j = 0;
            for (int i = 0; i < readyTeamsCount; i++)
            {
                TeamIndex teamIndex = playerTeamIndeces[i];
                TeamManager.instance.SetTeamExperience(teamIndex, TeamManager.instance.GetTeamExperience(TeamIndex.Player));
            }
            for (int i = 0; i < playersCount; i++, j++)
            {
                PlayerCharacterMasterController playerCharacterMasterController = PlayerCharacterMasterController.instances[i];
                if (playerCharacterMasterController == null) continue;
                CharacterMaster characterMaster = playerCharacterMasterController.master;
                if (characterMaster == null) continue;
                TeamIndex teamIndex = TeamIndex.None;
                while (j > readyTeamsCount - 1) j -= extraPlayerTeamsCount;
                teamIndex = playerTeamIndeces[j];
                TeamComponent teamComponent = characterMaster.GetBody() ? characterMaster.GetBody().teamComponent : null;
                if (teamComponent) teamComponent.teamIndex = teamIndex;
                characterMaster.teamIndex = teamIndex;
                ChangeMinionsTeam(playerCharacterMasterController, teamIndex);
            }
        }
        public static void EndPVP(bool punish)
        {
            if (!NetworkServer.active || !pvpEnabled) return;
            calling = false;
            int playersCount = GetAlivePlayers().Count;
            if (playersCount <= 1) return;
            playersCount = PlayerCharacterMasterController.instances.Count;
            pvpEnabled = false;
            startingPVP = false;
            networkUsers.Clear();
            int readyTeamsCount = playerTeamDefs.Count;
            for (int i = 0; i < playersCount; i++)
            {
                PlayerCharacterMasterController playerCharacterMasterController = PlayerCharacterMasterController.instances[i];
                if (playerCharacterMasterController == null) continue;
                CharacterMaster characterMaster = playerCharacterMasterController.master;
                if (characterMaster == null) continue;
                TeamIndex teamIndex = TeamIndex.Player;
                TeamComponent teamComponent = characterMaster.GetBody() ? characterMaster.GetBody().teamComponent : null;
                if (teamComponent) teamComponent.teamIndex = teamIndex;
                characterMaster.teamIndex = teamIndex;
                ChangeMinionsTeam(playerCharacterMasterController, teamIndex);
                if (!punish) continue;
                Inventory inventory = characterMaster.inventory;
                if (!inventory) continue;
                if (!characterMaster.IsDeadAndOutOfLivesServer()) continue;
                if (!losers.Contains(playerCharacterMasterController)) continue;
                losers.Remove(playerCharacterMasterController);
                int tonicAfflictionAmount = PVPLoserSpinelAfflictionsAmount.Value;
                if (tonicAfflictionAmount > 0)
                    inventory.GiveItem(RoR2Content.Items.TonicAffliction, tonicAfflictionAmount);
                List<ItemDef> list = [];
                int itemCount = 0;
                for (int j = 0; j < ItemCatalog.itemCount; j++)
                {
                    int itemStacks = inventory.itemStacks[j];
                    if (itemStacks <= 0) continue;
                    ItemDef itemDef = ItemCatalog.GetItemDef((ItemIndex)j);
                    if (itemDef == null || !itemDef.canRemove || itemDef.tier != PVPLoserLoseItemsRarity.Value) continue;
                    for (int k = 0; k < itemStacks; k++)
                    {
                        list.Add(itemDef);
                        itemCount++;
                    }
                }
                int loseItemsAmount = PVPLoserLoseItemsAmount.Value;
                for (int j = 0; j < loseItemsAmount && itemCount > 0; j++)
                {
                    ItemDef itemDef = list[UnityEngine.Random.Range(0, itemCount)];
                    inventory.RemoveItem(itemDef);
                    list.Remove(itemDef);
                    itemCount--;
                }
            }
        }
        public static void ChangeMinionsTeam(PlayerCharacterMasterController playerCharacterMasterController, TeamIndex teamIndex)
        {
            MinionOwnership.MinionGroup minionGroup = null;
            for (int i = 0; i < MinionOwnership.MinionGroup.instancesList.Count; i++)
            {
                MinionOwnership.MinionGroup minionGroup2 = MinionOwnership.MinionGroup.instancesList[i];
                if (MinionOwnership.MinionGroup.instancesList[i].ownerId == playerCharacterMasterController.netId)
                {
                    minionGroup = minionGroup2;
                    break;
                }
            }
            if (minionGroup == null) return;
            foreach (MinionOwnership minion in minionGroup.members)
            {
                if (minion == null) continue;
                CharacterMaster characterMaster = minion.GetComponent<CharacterMaster>();
                if (characterMaster == null) continue;
                characterMaster.teamIndex = teamIndex;
                CharacterBody characterBody = characterMaster.GetBody();
                if (characterBody == null) continue;
                TeamComponent teamComponent = characterBody.teamComponent;
                if (teamComponent == null) continue;
                teamComponent.teamIndex = teamIndex;
            }
        }
        public void FixedUpdate()
        {
            if (NetworkServer.active)
            if (startingPVP)
            {
                startingStopwatch -= Time.fixedDeltaTime;
                if (startingStopwatch <= 0f)
                {
                    BeginPVP();
                    startingPVP = false;
                }
            }
            if (Run.instance == null) return;
            TeamDef playerTeamDef = TeamCatalog.teamDefs[1];
            foreach (TeamDef teamDef in playerTeamDefs)
            {
                teamDef.softCharacterLimit = playerTeamDef.softCharacterLimit;
                teamDef.friendlyFireScaling = playerTeamDef.friendlyFireScaling;
                teamDef.levelUpEffect = playerTeamDef.levelUpEffect;
                teamDef.nameToken = playerTeamDef.nameToken;
            }
        }
        public static void Init()
        {
            playerTeamDefs.Clear();
            playerTeamIndeces.Clear();
            for (int i = 0; i < extraPlayerTeamsCount; i++) CreateTeam("Player" + (i + 1));
        }
        public static TeamIndex CreateTeam(string name)
        {
            TeamsAPI.TeamBehavior teamBehavior = new(name, TeamsAPI.TeamClassification.Player);
            string nameToken = "TEAM_" + name.ToUpper() + "_NAME";
            TeamDef teamDef = new()
            {
                softCharacterLimit = 20,
                friendlyFireScaling = 0.5f,
                levelUpEffect = null,
                nameToken = "TEAM_PLAYER_NAME"
            };
            TeamIndex teamIndex = TeamsAPI.RegisterTeam(teamDef, teamBehavior);
            playerTeamDefs.Add(teamDef);
            playerTeamIndeces.Add(teamIndex);
            return teamIndex;
        }
        public static int neededVotes;
        public static List<NetworkUser> networkUsers = [];
        public static bool calling;
        [ConCommand(commandName = "pvp_call", flags = ConVarFlags.ExecuteOnServer)]
        public static void CallPVPVote(ConCommandArgs args)
        {
            if (calling || pvpEnabled) return;
            int playersCount = GetAlivePlayers().Count;
            if (playersCount <= 1) return;
            neededVotes = playersCount - 1;
            networkUsers.Add(args.sender);
            calling = true;
            Chat.SendBroadcastChat(new Chat.UserChatMessage
            {
                sender = args.sender.gameObject,
                text = " has called for PVP. Type `pvp_accept` in the console to proceed. Needed votes: " + neededVotes
            });
        }
        [ConCommand(commandName = "pvp_accept", flags = ConVarFlags.ExecuteOnServer)]
        public static void AcceptPVPVote(ConCommandArgs args)
        {
            if (!calling)
            {
                Chat.SendBroadcastChat(new Chat.UserChatMessage
                {
                    sender = args.sender.gameObject,
                    text = " wanted to accept PVP call but it has not been called yet. Use `pvp_call` to call it."
                });
            }
            if (pvpEnabled || networkUsers.Contains(args.sender)) return;
            neededVotes--;
            networkUsers.Add(args.sender);
            if (neededVotes <= 0)
            {
                Chat.SendBroadcastChat(new Chat.UserChatMessage
                {
                    sender = args.sender.gameObject,
                    text = " has voted for PVP. Starting PVP!"
                });
                StartPVP();
                calling = false;
            }
            else
            {
                Chat.SendBroadcastChat(new Chat.UserChatMessage
                {
                    sender = args.sender.gameObject,
                    text = " has voted for PVP. Needed votes: " + neededVotes
                });
            }
        }
        [ConCommand(commandName = "pvp_end", flags = ConVarFlags.ExecuteOnServer)]
        public static void EndPVP(ConCommandArgs args)
        {
            if (!pvpEnabled) return;
            int playersCount = GetAlivePlayers().Count;
            if (playersCount <= 1) return;
            Chat.SendBroadcastChat(new Chat.UserChatMessage
            {
                sender = args.sender.gameObject,
                text = " has called for PVP. Type `pvp_accept` in the console to proceed. Needed votes: " + neededVotes
            });
            EndPVP(true);
        }
        public static List<PlayerCharacterMasterController> GetAlivePlayers()
        {
            List<PlayerCharacterMasterController> playerCharacterMasterControllers = [];
            foreach (PlayerCharacterMasterController playerCharacterMasterController in PlayerCharacterMasterController.instances)
            {
                if (playerCharacterMasterController.master && playerCharacterMasterController.master.IsDeadAndOutOfLivesServer()) continue;
                playerCharacterMasterControllers.Add(playerCharacterMasterController);
            }
            return playerCharacterMasterControllers;
        }
    }
    public class IncreaseDamageByShieldAndReduceShieldRechargeTimeBehaviour : CharacterBody.ItemBehavior
    {
        public void FixedUpdate()
        {
            HealthComponent healthComponent = body.healthComponent;
            if (healthComponent == null || healthComponent.isShieldRegenForced) return;
            float divisor = Utils.GetStackingFloat(PVPModPlugin.IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldRegenSpeedIncrease.Value, PVPModPlugin.IncreaseDamageByShieldAndReduceShieldRechargeTimeShieldRegenSpeedIncreasePerStack.Value, stack) / 100f + 1f;
            float finalValue = CharacterBody.outOfDangerDelay / divisor;
            if (body.outOfDangerStopwatch >= finalValue)
            {
                healthComponent.ForceShieldRegen();
                Util.PlaySound("Play_item_proc_personal_shield_recharge", gameObject);
            }
        }
    }
    public class PVPCountdown : MonoBehaviour
    {
        public TextMeshProUGUI textMeshProUGUI;
        public float countdown = 3f;
        public void Update()
        {
            countdown -= Time.deltaTime;
            if (countdown <= -1f)
            {
                Destroy(gameObject);
            }
            if (textMeshProUGUI)
            {
                textMeshProUGUI.text = countdown > 0 ? "PVP STARTS IN " + Mathf.Ceil(countdown) : "FIGHT!";
            }
        }
    }
    public class SpawnPVPCountdown : INetMessage
    {
        public float countdown;
        public SpawnPVPCountdown()
        {

        }
        public SpawnPVPCountdown(float countdown)
        {
            this.countdown = countdown;
        }
        public void Deserialize(NetworkReader reader)
        {
            countdown = reader.ReadSingle();
        }

        public void OnReceived()
        {
            GameObject gameObject = GameObject.Instantiate(PVPModPlugin.PVPCountdownPrefab);
            PVPCountdown pVPCountdown = gameObject.GetComponent<PVPCountdown>();
            pVPCountdown.countdown = countdown;
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(countdown);
        }
    }
    [CreateAssetMenu(menuName = "PVPMod/PVPModItemDef")]
    public class PVPModItemDef : ItemDef
    {
        public ItemTierSprite[] itemTierSprites;
        [Serializable]
        public struct ItemTierSprite
        {
            public ItemTier itemTier;
            public Sprite sprite;
        }
    }
    public class PVPModContentPack : IContentPackProvider
    {
        internal ContentPack contentPack = new ContentPack();
        public string identifier => PVPModPlugin.ModGuid + ".ContentProvider";
        public static List<ItemDef> items = [];
        public static List<BuffDef> buffs = [];
        public IEnumerator FinalizeAsync(FinalizeAsyncArgs args)
        {
            args.ReportProgress(1f);
            yield break;
        }

        public IEnumerator GenerateContentPackAsync(GetContentPackAsyncArgs args)
        {
            ContentPack.Copy(this.contentPack, args.output);
            args.ReportProgress(1f);
            yield break;
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            this.contentPack.identifier = this.identifier;
            contentPack.itemDefs.Add([.. items]);
            contentPack.buffDefs.Add([.. buffs]);
            yield break;
        }
    }
    public static class Utils
    {
        public const string damagePrefix = "<style=cIsDamage>";
        public const string stackPrefix = "<style=cStack>";
        public const string utilityPrefix = "<style=cIsUtility>";
        public const string healingPrefix = "<style=cIsHealing>";
        public const string endPrefix = "</style>";
        public static void AddLanguageToken(string token, string value) => AddLanguageToken(token, value, "en");
        public static void AddLanguageToken(string token, string value, string language)
        {
            Language language1 = Language.languagesByName[language];
            if (language1.stringsByToken.ContainsKey(token))
            {
                language1.stringsByToken[token] = value;
            }
            else
            {
                language1.stringsByToken.Add(token, value);
            }
        }
        public static ConfigEntry<T> CreateConfig<T>(string section, string field, T defaultValue, string description) => CreateConfig(section, field, defaultValue, description, false);
        public static ConfigEntry<T> CreateConfig<T>(string section, string field, T defaultValue, string description, bool riskOfOptionsRestartRequired)
        {
            ConfigEntry<T> configEntry = PVPModPlugin.configFile.Bind(section, field, defaultValue, description);
            if (PVPModPlugin.riskOfOptionsEnabled) ModCompatabilities.RiskOfOptionsCompatAbility.HandleConfig(configEntry, defaultValue, riskOfOptionsRestartRequired);
            PVPModPlugin.configs.Add(configEntry);
            return configEntry;
        }
        public static T RegisterItemDef<T>(this T itemDef, Action<ItemDef> onItemDefAdded = null) where T : ItemDef
        {
            ConfigEntry<bool> enableConfig = CreateConfig(itemDef.name, "Enable", true, "Enable this item?", true);
            ConfigEntry<ItemTier> tierConfig = CreateConfig(itemDef.name, "Rarity", itemDef.tier, "Control rarity of this item", true);
            PVPModItemDef pVPModItemDef = itemDef as PVPModItemDef;
            if (pVPModItemDef)
            {
                foreach (PVPModItemDef.ItemTierSprite itemTierSprite in pVPModItemDef.itemTierSprites)
                {
                    if (itemTierSprite.itemTier == tierConfig.Value)
                    {
                        pVPModItemDef.pickupIconSprite = itemTierSprite.sprite;
                        pVPModItemDef.deprecatedTier = tierConfig.Value;
                        break;
                    }
                }
            }
            if (enableConfig == null || enableConfig.Value == true)
            {
                PVPModContentPack.items.Add(itemDef);
                onItemDefAdded?.Invoke(itemDef);
            }
            return itemDef;
        }
        public static T RegisterBuffDef<T>(this T buffDef, Action<BuffDef> onBuffDefAdded = null) where T : BuffDef
        {
            PVPModContentPack.buffs.Add(buffDef);
            onBuffDefAdded?.Invoke(buffDef);
            return buffDef;
        }
        public static float GetStackingFloat(float baseValue, float stackValue, int stacks) => baseValue + (stacks - 1) * stackValue;
    }
    public static class ModCompatabilities
    {
        public static class RiskOfOptionsCompatAbility
        {
            public const string ModGUID = "com.rune580.riskofoptions";
            public static void HandleConfig<T>(ConfigEntry<T> configEntry, T value, bool restartRequired)
            {
                if (value is float) ModSettingsManager.AddOption(new FloatFieldOption(configEntry as ConfigEntry<float>, restartRequired));
                if (value is bool)ModSettingsManager.AddOption(new CheckBoxOption(configEntry as ConfigEntry<bool>, restartRequired));
                if (value is int)ModSettingsManager.AddOption(new IntFieldOption(configEntry as ConfigEntry<int>, restartRequired));
                if (value is string)ModSettingsManager.AddOption(new StringInputFieldOption(configEntry as ConfigEntry<string>, restartRequired));
                if (value is Enum)
                {
                    Enum @enum = value as Enum;
                    if (@enum.GetType().GetCustomAttributes<FlagsAttribute>().Any()) return;
                    ModSettingsManager.AddOption(new ChoiceOption(configEntry, restartRequired));
                }
            }
        }
    }
}