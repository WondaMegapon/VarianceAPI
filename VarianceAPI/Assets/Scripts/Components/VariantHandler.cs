﻿using EntityStates;
using KinematicCharacterController;
using RoR2;
using RoR2.CharacterAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using VarianceAPI.Modules;
using VarianceAPI.Scriptables;
using Logger = VarianceAPI.MainClass;

namespace VarianceAPI.Components
{
    public class VariantHandler : NetworkBehaviour
    {
        [SyncVar]
        public bool isVariant = false;

        public bool unique;
        public bool givesRewards;

        public string identifierName;
        public string bodyName;
        public string arrivalMessage;
        public string customDeathState;

        public float spawnRate = 1f;
        public float healthModifier = 1f;
        public float moveSpeedModifier = 1f;
        public float attackSpeedModifier = 1f;
        public float damageModifier = 1f;
        public float armorModifier = 1f;
        public float armorBonus = 0f;

        public VariantTier tier;
        public VariantAIModifier aiModifier;

        public VariantMaterialReplacement[] materialReplacements;
        public VariantLightReplacement[] lightReplacements;
        public VariantMeshReplacement[] meshReplacements;
        public VariantSkillReplacement[] skillReplacements;
        public VariantOverrideName[] overrideNames;
        public VariantExtraComponent[] extraComponents;
        public VariantSizeModifier sizeModifier;
        public CustomVariantReward customVariantReward;

        public VariantBuff[] buff;
        public VariantInventory inventory;
        public EquipmentInfo customEquipment;
        public GameObject thisGameObject;
        public string announcement;

        private CharacterBody body;
        private CharacterMaster master;
        private EquipmentIndex storedEquipment;
        private DeathRewards deathRewards;
        private ItemDisplayRuleSet storedIDRS;
        private CharacterDeathBehavior deathBehavior;

        public void Init(VariantInfo info)
        {
            this.unique = info.unique;
            this.givesRewards = info.givesRewards;

            this.identifierName = info.identifierName;
            this.bodyName = info.bodyName;
            this.overrideNames = info.overrideName;
            this.arrivalMessage = info.arrivalMessage;
            this.customDeathState = info.customDeathState;

            this.spawnRate = info.spawnRate;
            this.healthModifier = info.healthMultiplier;
            this.moveSpeedModifier = info.moveSpeedMultiplier;
            this.attackSpeedModifier = info.attackSpeedMultiplier;
            this.damageModifier = info.damageMultiplier;
            this.armorModifier = info.armorMultiplier;
            this.armorBonus = info.armorBonus;

            this.tier = info.variantTier;
            this.aiModifier = info.aiModifier;

            this.materialReplacements = info.materialReplacement;
            this.lightReplacements = info.lightReplacement;
            this.meshReplacements = info.meshReplacement;
            this.skillReplacements = info.skillReplacement;
            this.extraComponents = info.extraComponents;
            this.sizeModifier = info.sizeModifier;
            this.customVariantReward = info.customVariantReward;

            this.buff = info.buff;

            this.inventory = info.variantInventory;
            this.customEquipment = info.customEquipment;
            this.announcement = info.arrivalMessage;
        }

        private void Awake()
        {
            if (!NetworkServer.active)
            {
                return;
            }
            //Run artifact code ONLY if it's enabled in the config loader.
            if(ConfigLoader.EnableArtifactOfVariance.Value)
            {
                if (RunArtifactManager.instance.IsArtifactEnabled(ContentPackProvider.contentPack.artifactDefs.Find("VarianceDef")))
                {
                    this.spawnRate *= ConfigLoader.VarianceMultiplier.Value;
                    //Prevent bad values
                    if (this.spawnRate < 0)
                    {
                        this.spawnRate = 0;
                    }
                    else if (this.spawnRate > 100)
                    {
                        this.spawnRate = 100;
                    }
                }
            }
            if (Util.CheckRoll(this.spawnRate))
            {
                this.isVariant = true;
                thisGameObject = this.gameObject;
            }
            if (this.meshReplacements != null && this.isVariant)
            {
                if (this.meshReplacements.Length > 0)
                {
                    this.storedIDRS = this.GetComponentInChildren<CharacterModel>().itemDisplayRuleSet;
                    this.GetComponentInChildren<CharacterModel>().itemDisplayRuleSet = null;
                }
            }
        }

        private void Start()
        {
            if (this.isVariant)
            {
                if (this.unique)
                {
                    Logger.Log.LogInfo(identifierName + " is unique, attempting to remove unecesary componenets.");
                    foreach (VariantHandler i in this.GetComponents<VariantHandler>())
                    {
                        if (i && i != this)
                        {
                            Destroy(i);
                        }
                    }
                }

                this.body = base.GetComponent<CharacterBody>();
                if (this.body)
                {
                    if (this.tier >= VariantTier.Rare && this.arrivalMessage != "")
                    {
                        if (this.arrivalMessage != "")
                        {
                            Chat.AddMessage(announcement);
                        }
                        else
                        {
                            Logger.Log.LogMessage(identifierName + " Variant is Rare or Legendary but doesnt have an arrival message set! using generic message.");
                            Chat.AddMessage("A " + body.GetDisplayName() + " with unique qualities has appeared!");
                        }
                    }
                    this.master = this.body.master;
                    this.deathBehavior = base.GetComponent<CharacterDeathBehavior>();

                    if (this.master)
                    {
                        this.ApplyBuffs();
                        if (this.aiModifier.HasFlag(VariantAIModifier.Unstable))
                        {
                            foreach (AISkillDriver i in this.master.GetComponents<AISkillDriver>())
                            {
                                if (i)
                                {
                                    i.minTargetHealthFraction = Mathf.NegativeInfinity;
                                    i.maxTargetHealthFraction = Mathf.Infinity;
                                    i.minUserHealthFraction = Mathf.NegativeInfinity;
                                    i.maxUserHealthFraction = Mathf.Infinity;
                                }
                            }
                        }

                        if (this.aiModifier.HasFlag(VariantAIModifier.ForceSprint))
                        {
                            foreach (AISkillDriver i in this.master.GetComponents<AISkillDriver>())
                            {
                                if (i)
                                {
                                    i.shouldSprint = true;
                                }
                            }
                        }
                    }
                    if (ConfigLoader.VariantsGiveRewards.Value)
                    {
                        if (givesRewards)
                        {
                            if (customVariantReward != null)
                            {
                                if (thisGameObject.GetComponent<VariantRewardHandler>())
                                {
                                    
                                }
                                else
                                {
                                    thisGameObject.AddComponent<VariantRewardHandler>().InitCustomRewards(customVariantReward);
                                }
                            }
                            else
                            {
                                if (thisGameObject.GetComponent<VariantRewardHandler>())
                                {
                                }
                                else
                                {
                                    thisGameObject.AddComponent<VariantRewardHandler>().Init();
                                }
                            }
                        }
                    }
                }
            }
        }
        private void ApplyBuffs()
        {
            this.ModifyStats();
            this.AddItems();
            this.AddEquipment();
            this.ModifyModel();
            this.SwapSkills();
            this.AddExtraComponents();
            this.ModifyName();
            this.ReplaceDeathState();
            this.AddBuffs();

            //Change Size
            this.ScaleBody();

            this.body.healthComponent.health = this.body.healthComponent.fullHealth;
            this.body.RecalculateStats();
        }
        private void ModifyStats()
        {
            List<float> multipliers = new List<float>() { this.healthModifier, this.moveSpeedModifier, this.attackSpeedModifier, this.damageModifier, this.armorModifier };
            foreach(float multiplier in multipliers)
            {
                if(multiplier < 0)
                {
                    Logger.Log.LogWarning("One of " + this.identifierName + "'s stat multipliers is set to a negative number! Continuing but expect unstable things, here be dragons!");
                }
            }
            this.body.baseMaxHealth *= this.healthModifier;
            this.body.baseMoveSpeed *= this.moveSpeedModifier;
            this.body.baseAttackSpeed *= this.attackSpeedModifier;
            this.body.baseDamage *= this.damageModifier;
            this.body.levelDamage = this.body.baseDamage * 0.2f;
            this.body.baseArmor *= this.armorModifier;
            this.body.baseArmor += this.armorBonus;
        }

        private void AddItems()
        {
            if (this.master.inventory)
            {
                if (this.inventory != null)
                {
                    if(inventory.itemStrings.Length == inventory.counts.Length)
                    {
                        for (int i = 0; i < inventory.counts.Length; i++)
                        {
                            bool giveItem = true;
                            if (inventory.itemStrings[i] == "ExtraLife")
                            {
                                if (this.master.GetComponent<PreventRecursion>())
                                {
                                    giveItem = false;
                                }
                                else
                                {
                                    this.master.gameObject.AddComponent<PreventRecursion>();
                                }
                            }
                            if (giveItem)
                            {
                                ItemDef itemDef = ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex(inventory.itemStrings[i]));
                                var amount = inventory.counts[i];
                                if(itemDef != null && amount > 0)
                                {
                                    this.master.inventory.GiveItemString(inventory.itemStrings[i], inventory.counts[i]);
                                }
                                else
                                {
                                    if(itemDef == null)
                                    {
                                        Logger.Log.LogWarning("Could not find itemDef matching ItemString \"" + inventory.itemStrings[i] + "\". Aborting adding said item.");
                                    }
                                    if(amount <= 0)
                                    {
                                        Logger.Log.LogWarning(inventory.itemStrings[i] + "'s amount was 0 or lower.");
                                    }
                                }   
                            }
                        }
                    }
                    else
                    {
                        Logger.Log.LogError(identifierName + "'s VariantInventory's arrays are not the same size! aborting adding items from the inventory!");
                    }
                }
                //Makes healthbars purple
                if (this.tier >= VariantTier.Uncommon)
                {
                    this.master.inventory.GiveItem(ContentPackProvider.contentPack.itemDefs.Find("VAPI_PurpleHealthbar"));
                }
            }
        }
        private void AddEquipment()
        {
            if(this.master.inventory)
            {
                if(customEquipment)
                {
                    EquipmentDef equipmentDef = EquipmentCatalog.GetEquipmentDef(EquipmentCatalog.FindEquipmentIndex(customEquipment.equipmentString));
                    if(equipmentDef)
                    {
                        master.inventory.GiveEquipmentString(customEquipment.equipmentString);
                        thisGameObject.AddComponent<VariantEquipmentHandler>().equipmentInfo = customEquipment;
                    }
                    else
                    {
                        Logger.Log.LogWarning("Could not find EquipmentDef matching EquipmentString \"" + customEquipment.equipmentString + "\". Aborting adding said Equipment.");
                    }
                }
            }
        }
        private void ModifyModel()
        {
            //Grab Model
            CharacterModel model = null;
            ModelLocator modelLocator = this.body.GetComponent<ModelLocator>();
            if (modelLocator)
            {
                Transform modelTransform = modelLocator.modelTransform;
                if (modelTransform)
                {
                    model = modelTransform.GetComponent<CharacterModel>();
                }
            }
            if (model)
            {
                if (this.materialReplacements == null)
                {
                    this.materialReplacements = new VariantMaterialReplacement[0];
                }
                if (this.meshReplacements == null)
                {
                    this.meshReplacements = new VariantMeshReplacement[0];
                }
                if(this.lightReplacements == null)
                {
                    this.lightReplacements = new VariantLightReplacement[0];
                }

                //Replace Materials
                if (this.materialReplacements.Length > 0)
                {
                    for (int i = 0; i < this.materialReplacements.Length; i++)
                    {
                        model.baseRendererInfos[this.materialReplacements[i].rendererIndex].defaultMaterial = this.materialReplacements[i].material;
                    }
                }
                //Replaces lights
                if(this.lightReplacements.Length > 0)
                {
                    for(int i = 0; i< this.lightReplacements.Length; i++)
                    {
                        model.baseLightInfos[this.lightReplacements[i].rendererIndex].defaultColor = this.lightReplacements[i].color;
                    }
                }

                //Replace Meshes
                if (this.meshReplacements.Length > 0)
                {
                    this.TryFuckWithBoneStructure();

                    for (int i = 0; i < this.meshReplacements.Length; i++)
                    {
                        model.baseRendererInfos[this.meshReplacements[i].rendererIndex].renderer.GetComponent<SkinnedMeshRenderer>().sharedMesh = this.meshReplacements[i].mesh;
                    }
                }
            }
        }
        private void SwapSkills()
        {
            if (this.skillReplacements == null)
            {
                return;
            }

            SkillLocator skillLocator = this.body.skillLocator;

            if (skillLocator)
            {
                for (int i = 0; i < skillReplacements.Length; i++)
                {
                    switch (skillReplacements[i].skillSlot)
                    {
                        case SkillSlot.Primary:
                            skillLocator.primary.SetSkillOverride(this.gameObject, skillReplacements[i].skillDef, GenericSkill.SkillOverridePriority.Upgrade);
                            break;
                        case SkillSlot.Secondary:
                            skillLocator.secondary.SetSkillOverride(this.gameObject, skillReplacements[i].skillDef, GenericSkill.SkillOverridePriority.Upgrade);
                            break;
                        case SkillSlot.Utility:
                            skillLocator.utility.SetSkillOverride(this.gameObject, skillReplacements[i].skillDef, GenericSkill.SkillOverridePriority.Upgrade);
                            break;
                        case SkillSlot.Special:
                            skillLocator.special.SetSkillOverride(this.gameObject, skillReplacements[i].skillDef, GenericSkill.SkillOverridePriority.Upgrade);
                            break;
                        case SkillSlot.None:
                            Logger.Log.LogWarning(identifierName + "'s " + i + " skill replacement has its skill slot set to none.");
                            break;
                    }
                }
            }
        }
        private void AddExtraComponents()
        {
            for (int i = 0; i < extraComponents.Length; i++)
            {
                VariantExtraComponent extraComponent = extraComponents[i];
                if (extraComponent.isAesthetic)
                {
                    ModelLocator modelLocator = this.body.GetComponent<ModelLocator>();
                    if (modelLocator)
                    {
                        Transform modelTransform = modelLocator.modelTransform;
                        if (modelTransform)
                        {
                            Type type;
                            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(Assembly => Assembly.GetReferencedAssemblies().Any(AssName => AssName.FullName == "VarianceAPI, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"));
                            if (assemblies != null)
                            {
                                foreach (var assembly in assemblies)
                                {
                                    type = assembly.GetType(extraComponent.componentToAdd);
                                    if (type != null)
                                    {
                                        if (typeof(VariantComponent).IsAssignableFrom(type))
                                        {
                                            Logger.Log.LogMessage("Adding " + type.Name + " Component to " + body.GetDisplayName());
                                            modelTransform.gameObject.AddComponent(type);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    Logger.Log.LogMessage("Variant Components that are not aesthetic have yet to be implemented, we're sorry for this inconvenience.");
                }
            }
        }

        private void ModifyName()
        {
            if (this.overrideNames != null)
            {
                for (int i = 0; i < overrideNames.Length; i++)
                {
                    VariantOverrideName overrideName = overrideNames[i];
                    switch (overrideName.overrideType)
                    {
                        case OverrideNameType.Preffix:
                            this.body.baseNameToken = overrideName.textToAdd + " " + body.GetDisplayName();
                            return;
                        case OverrideNameType.Suffix:
                            this.body.baseNameToken = body.GetDisplayName() + " " + overrideName.textToAdd;
                            return;
                        case OverrideNameType.CompleteOverride:
                            this.body.baseNameToken = overrideName.textToAdd;
                            return;
                    }
                }
            }
        }
        private void ReplaceDeathState()
        {
            if(this.customDeathState != "")
            {
                deathBehavior.deathState = new SerializableEntityStateType(customDeathState);
            }
        }

        private void AddBuffs()
        {
            if(this.body)
            {
                if(this.buff != null)
                {
                    for (int i = 0; i < buff.Length; i++)
                    {
                        var currentBuffIndex = buff[i];

                        var BuffToGive = BuffCatalog.GetBuffDef(BuffCatalog.FindBuffIndex(currentBuffIndex.buffDef));
                        if(BuffToGive != null)
                        {
                            if(currentBuffIndex.isTimed)
                            {
                                if(currentBuffIndex.stacks == 0)
                                {
                                    this.body.AddTimedBuff(BuffToGive, currentBuffIndex.time);
                                }
                                else
                                {
                                    this.body.AddTimedBuff(BuffToGive, currentBuffIndex.time, currentBuffIndex.stacks);
                                }
                            }
                            else
                            {
                                this.body.AddBuff(BuffToGive);
                            }
                        }
                        else
                        {
                            Logger.Log.LogWarning("Could not find BuffDef matching BuffString \"" + currentBuffIndex.buffDef + "\". Aborting adding said Buff.");
                        }
                    }
                }
            }
        }
        private void ScaleBody()
        {
            if(this.sizeModifier == null)
            {
                return;
            }

            ModelLocator modelLocator = this.body.GetComponent<ModelLocator>();
            if(modelLocator)
            {
                Transform modelTransform = modelLocator.modelBaseTransform;
                if (modelTransform)
                {
                    modelTransform.localScale *= this.sizeModifier.newSize;

                    if (this.sizeModifier.scaleCollider)
                    {
                        foreach (KinematicCharacterMotor kinematicCharacterMotor in this.body.GetComponentsInChildren<KinematicCharacterMotor>())
                        {
                            if (kinematicCharacterMotor) kinematicCharacterMotor.SetCapsuleDimensions(kinematicCharacterMotor.Capsule.radius * this.sizeModifier.newSize, kinematicCharacterMotor.Capsule.height * this.sizeModifier.newSize, this.sizeModifier.newSize);
                        }
                    }
                }
            }
        }
        private void RestoreEquipment()
        {
            CharacterModel model = null;
            ModelLocator modelLocator = this.body.GetComponent<ModelLocator>();
            if (modelLocator)
            {
                Transform modelTransform = modelLocator.modelTransform;
                if (modelTransform)
                {
                    model = modelTransform.GetComponent<CharacterModel>();
                }

                if (model && this.storedIDRS != null)
                {
                    model.itemDisplayRuleSet = this.storedIDRS;
                }

                this.master.inventory.SetEquipmentIndex(this.storedEquipment);
            }
        }
        //No clue what this is for, used for beetles apparently, will need to test why rob had to fuck with the bone structure
        private void TryFuckWithBoneStructure()
        {
            this.storedEquipment = this.master.inventory.GetEquipmentIndex();
            this.master.inventory.SetEquipmentIndex(EquipmentIndex.None);
            this.Invoke("RestoreEquipment", 0.2f);

            foreach (VariantMeshReplacement meshReplacement in meshReplacements)
            {
                switch(meshReplacement.meshType)
                {
                    case MeshType.Default:
                        RestoreEquipment();
                        break;
                    case MeshType.Beetle:
                        FuckBoneStructure(MeshType.Beetle);
                        RestoreEquipment();
                        break;
                    case MeshType.BeetleGuard:
                        Logger.Log.LogWarning("Beetle Guard MeshSwaps are not supported! your variant will not appear correctly ingame.");
                        RestoreEquipment();
                        break;
                    case MeshType.MiniMushrum:
                        FuckBoneStructure(MeshType.MiniMushrum);
                        RestoreEquipment();
                        break;
                    case MeshType.MagmaWorm:
                        Logger.Log.LogWarning("MagmaWorm MeshSwaps are not supported! your variant will not appear correctly ingame.");
                        RestoreEquipment();
                        break;
                    case MeshType.OverloadingWorm:
                        Logger.Log.LogWarning("Overloading Worm MeshSwaps are not supported! your variant will not appear correctly ingame.");
                        RestoreEquipment();
                        break;
                }
            }
        }
        //Code courtesy of the guys at Moisture upset
        private void FuckBoneStructure(MeshType meshType)
        {
            if(meshType == MeshType.Beetle)
            {
                List<Transform> transforms = new List<Transform>();
                foreach (var item in body.GetComponentsInChildren<Transform>())
                {
                    if(!item.name.Contains("Hurtbox") && !item.name.Contains("BeetleBody") && !item.name.Contains("Mesh") && !item.name.Contains("mdl"))
                    {
                        transforms.Add(item);
                    }
                }

                Transform temp = transforms[14];
                transforms[14] = transforms[11];
                transforms[11] = temp;
                temp = transforms[15];
                transforms[15] = transforms[12];
                transforms[12] = temp;
                temp = transforms[16];
                transforms[16] = transforms[13];
                transforms[13] = temp;
                foreach (var item in body.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    item.bones = transforms.ToArray();
                }
            }
            else if (meshType == MeshType.BeetleGuard)
            {
                List<Transform> transforms = new List<Transform>();
                foreach (var item in body.GetComponentsInChildren<Transform>())
                {
                    if (!item.name.Contains("Hurtbox") && !item.name.Contains("IK") && !item.name.Contains("_end"))
                    {
                        transforms.Add(item);
                    }
                }
                foreach (var item in body.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    item.bones = transforms.ToArray();
                }
                transforms.Clear();
            }
            else if(meshType == MeshType.MiniMushrum)
            {
                List<Transform> transforms = new List<Transform>();
                foreach (var item in body.GetComponentsInChildren<Transform>())
                {
                    if(!item.name.Contains("Hurtbox") && !item.name.Contains("IK") && !item.name.Contains("_end") && !item.name.Contains("miniMush_R_Palps_02"))
                    {
                        transforms.Add(item);
                    }
                }
                for (int i = 0; i < 7; i++)
                {
                    transforms.RemoveAt(transforms.Count - 1);
                }
                foreach (var item in body.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    item.bones = transforms.ToArray();
                }
            }
        }
    }
}
