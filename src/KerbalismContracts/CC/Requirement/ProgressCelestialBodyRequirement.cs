﻿using ContractConfigurator;
using KSPAchievements;

namespace KerbalismContracts
{
    /// <summary>
    /// Base class for all ContractRequirement classes that use a celestial body.
    /// </summary>
    public abstract class ProgressCelestialBodyRequirement : ContractRequirement
    {
        enum CheckType
        {
            UNMANNED,
            MANNED
        }
        CheckType? checkType;

        public override bool LoadFromConfig(ConfigNode configNode)
        {
            // Load base class
            bool valid = base.LoadFromConfig(configNode);

            valid &= ValidateTargetBody(configNode);
            valid &= ConfigNodeUtil.ParseValue<CheckType?>(configNode, "checkType", x => checkType = x, this, (CheckType?)null);

            return valid;
        }

        public override void OnSave(ConfigNode configNode)
        {
            if (checkType != null)
            {
                configNode.AddValue("checkType", checkType);
            }
        }

        public override void OnLoad(ConfigNode configNode)
        {
            checkType = ConfigNodeUtil.ParseValue<CheckType?>(configNode, "checkType", (CheckType?)null);
        }

        protected ProgressNode GetCelestialBodySubtree()
        {
            // Get the progress tree for our celestial body
            foreach (var node in ProgressTracking.Instance.celestialBodyNodes)
            {
                if (node.Body == targetBody)
                {
                    return GetTypeSpecificProgressNode(node);
                }
            }

            return null;
        }

        protected abstract ProgressNode GetTypeSpecificProgressNode(CelestialBodySubtree celestialBodySubtree);

        public override bool RequirementMet(ConfiguredContract contract)
        {
            // Perform another validation of the target body to catch late validation issues due to expressions
            if (!ValidateTargetBody())
            {
                return false;
            }

            // Validate the CelestialBodySubtree exists
            ProgressNode cbProgress = GetCelestialBodySubtree();
            if (cbProgress == null)
            {
                LoggingUtil.LogError(this, ": ProgressNode for targetBody " + targetBody.bodyName + " not found.");
                return false;
            }

            if (checkType == CheckType.MANNED)
            {
                return cbProgress.IsReached && cbProgress.IsCompleteManned;
            }
            else if (checkType == CheckType.UNMANNED)
            {
                return cbProgress.IsReached && cbProgress.IsCompleteUnmanned;
            }

            return true;
        }

        protected string CheckTypeString()
        {
            return checkType == null ? "" : checkType == CheckType.MANNED ? "crewed " : "uncrewed ";
        }

        protected string ACheckTypeString()
        {
            return checkType == null ? "a " : checkType == CheckType.MANNED ? "a crewed " : "an uncrewed ";
        }

        protected string AnCheckTypeString()
        {
            return checkType == null ? "an " : checkType == CheckType.MANNED ? "a crewed " : "an uncrewed ";
        }
    }
}
