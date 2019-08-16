﻿using System;
using System.Collections.Generic;
using Contracts;
using KSP.Localization;
using ContractConfigurator;
using ContractConfigurator.Parameters;

namespace Kerbalism.Contracts
{
	public enum RadiationField { UNDEFINED, INNER_BELT, OUTER_BELT, MAGNETOPAUSE, ANY }

	public class RadiationFieldFactory : ParameterFactory
	{
		protected RadiationField field;
		protected int crossings_min, crossings_max;
		protected bool stay_in, stay_out;

		public override bool Load(ConfigNode configNode)
		{
			bool valid = base.Load(configNode);

			valid &= ConfigNodeUtil.ParseValue<RadiationField>(configNode, "field", x => field = x, this, RadiationField.UNDEFINED, ValidateField);
			valid &= ConfigNodeUtil.ParseValue<int>(configNode, "crossings_min", x => crossings_min = x, this, -1);
			valid &= ConfigNodeUtil.ParseValue<int>(configNode, "crossings_max", x => crossings_max = x, this, -1);
			valid &= ConfigNodeUtil.ParseValue<bool>(configNode, "stay_in", x => stay_in = x, this, false);
			valid &= ConfigNodeUtil.ParseValue<bool>(configNode, "stay_out", x => stay_out = x, this, false);

			valid &= ValidateTargetBody(configNode);
			valid &= ConfigNodeUtil.MutuallyExclusive(configNode, new string[] { "stay_in", "stay_out" }, new string[] { "crossings_min", "crossings_max" }, this);

			if (crossings_max > 0 && crossings_min > crossings_max)
			{
				LoggingUtil.LogError(GetType(), ErrorPrefix() + ": crossings_min must be <= crossings_max");
				valid = false;
			}

			if (stay_in && stay_out)
			{
				LoggingUtil.LogError(GetType(), ErrorPrefix() + ": cannot be both stay_in AND stay_out");
				valid = false;
			}

			return valid;
		}

		public override ContractParameter Generate(Contract contract)
		{
			if (!ValidateTargetBody())
			{
				return null;
			}

			return new RadiationFieldParameter(field, crossings_min, crossings_max, stay_in, stay_out, targetBody, title);
		}

		private bool ValidateField(RadiationField f)
		{
			if(f == RadiationField.UNDEFINED)
			{
				LoggingUtil.LogError(this, "Missing field. You must specify field = INNER_BELT, OUTER_BELT, MAGNETOPAUSE or ANY.");
				return false;
			}
			return true;
		}

	}

	public class RadiationFieldParameter : VesselParameter
	{
		public RadiationField field;
		protected int crossings_min;
		protected int crossings_max;
		protected bool stay_in;
		protected bool stay_out;

		protected int crossed_count = 0;
		protected bool currently_in_field = false;

		public RadiationFieldParameter(): base(null) {}

		public RadiationFieldParameter(RadiationField field, int crossings_min, int crossings_max, bool stay_in, bool stay_out, CelestialBody targetBody, string title)
		{
			this.field = field;
			this.crossings_min = crossings_min;
			this.crossings_max = crossings_max;
			this.stay_in = stay_in;
			this.stay_out = stay_out;
			this.targetBody = targetBody;
			this.title = title;
		}

		protected override string GetParameterTitle()
		{
			if (!string.IsNullOrEmpty(title)) return title;

			string bodyName = targetBody != null ? targetBody.CleanDisplayName() : "a body";
			return "Find " + FieldName(field) + " of " + bodyName;
		}

		public static String FieldName(RadiationField field)
		{
			switch (field)
			{
				case RadiationField.INNER_BELT: return "inner radiation belt";
				case RadiationField.OUTER_BELT: return "outer radiation belt";
				case RadiationField.MAGNETOPAUSE: return "magnetopause";
				case RadiationField.ANY: return "any radiation field";
			}
			return "INVALID FIELD TYPE";
		}

		protected static bool InField(Vessel v, RadiationField f)
		{
			switch(f)
			{
				case RadiationField.INNER_BELT: return RadiationFieldTracker.InnerBelt(v);
				case RadiationField.OUTER_BELT: return RadiationFieldTracker.OuterBelt(v);
				case RadiationField.MAGNETOPAUSE: return RadiationFieldTracker.Magnetosphere(v);
				case RadiationField.ANY:
					return InField(v, RadiationField.INNER_BELT) || InField(v, RadiationField.OUTER_BELT) || InField(v, RadiationField.MAGNETOPAUSE);
			}
			return false;
		}

		protected override void OnParameterSave(ConfigNode node)
		{
			base.OnParameterSave(node);

			node.AddValue("field", field);
			node.AddValue("crossed_count", crossed_count);
			node.AddValue("currently_in_field", currently_in_field);
			node.AddValue("targetBody", targetBody.name);
			node.AddValue("stay_in", stay_in);
			node.AddValue("stay_out", stay_out);
			node.AddValue("crossings_min", crossings_min);
			node.AddValue("crossings_max", crossings_max);
		}

		protected override void OnParameterLoad(ConfigNode node)
		{
			try
			{
				base.OnParameterLoad(node);

				field = ConfigNodeUtil.ParseValue<RadiationField>(node, "field", RadiationField.UNDEFINED);
				crossed_count = ConfigNodeUtil.ParseValue<int>(node, "crossed_count", 0);
				currently_in_field = ConfigNodeUtil.ParseValue<bool>(node, "currently_in_field", false);
				targetBody = ConfigNodeUtil.ParseValue<CelestialBody>(node, "targetBody", (CelestialBody)null);
				stay_in = ConfigNodeUtil.ParseValue<bool>(node, "stay_in", false);
				stay_out = ConfigNodeUtil.ParseValue<bool>(node, "stay_out", false);
				crossings_min = ConfigNodeUtil.ParseValue<int>(node, "crossings_min", 0);
				crossings_max = ConfigNodeUtil.ParseValue<int>(node, "crossings_max", 0);
			}
			finally
			{
				ParameterDelegate<Vessel>.OnDelegateContainerLoad(node);
			}
		}

		protected override void OnRegister()
		{
			base.OnRegister();
			RadiationFieldTracker.AddListener(RunCheck);
		}

		protected override void OnUnregister()
		{
			base.OnUnregister();
			RadiationFieldTracker.RemoveListener(RunCheck);
		}

		private void RunCheck(Vessel v, bool inner_belt, bool outer_belt, bool magnetosphere)
		{
			Lib.Log("RunCheck " + v + ": inner " + inner_belt + " outer " + outer_belt + " magneto " + magnetosphere);
			CheckVessel(v);
		}

		protected override bool VesselMeetsCondition(Vessel vessel)
		{
			if (vessel == null) return false;

			if (targetBody != null && vessel.mainBody != targetBody) return false;

			bool in_field = InField(vessel, field);

			if (stay_in && in_field) return false;
			if (stay_out && in_field) return false;
			if (stay_in || stay_out) return true;

			if (in_field != currently_in_field) crossed_count++;
			currently_in_field = in_field;

			if (crossings_min >= 0 && crossed_count < crossings_min) return false;
			if (crossings_max >= 0 && crossed_count > crossings_max) return false;
			return true;
		}
	}

	public class RadiationFieldVisibleFactory : ParameterFactory
	{
		protected RadiationField field;

		public override bool Load(ConfigNode configNode)
		{
			bool valid = base.Load(configNode);

			valid &= ConfigNodeUtil.ParseValue<RadiationField>(configNode, "field", x => field = x, this, RadiationField.ANY);
			valid &= ValidateTargetBody(configNode);

			return valid;
		}

		public override ContractParameter Generate(Contract contract)
		{
			if (!ValidateTargetBody())
			{
				return null;
			}

			return new RadiationFieldVisibleParameter(field, targetBody, title);
		}
	}

	public class RadiationFieldVisibleParameter : ContractConfiguratorParameter
	{
		public RadiationField field;

		public RadiationFieldVisibleParameter() : base(null) { }

		public RadiationFieldVisibleParameter(RadiationField field, CelestialBody targetBody, string title)
		{
			this.field = field;
			this.targetBody = targetBody;
			this.title = title;
		}

		protected override string GetParameterTitle()
		{
			if (!string.IsNullOrEmpty(title)) return title;

			string bodyName = targetBody != null ? targetBody.CleanDisplayName() : "a body";
			return RadiationFieldParameter.FieldName(field) + " of " + bodyName + " is researched";
		}

		protected override void OnParameterSave(ConfigNode node)
		{
			node.AddValue("field", field);
			node.AddValue("targetBody", targetBody.name);
		}

		protected override void OnParameterLoad(ConfigNode node)
		{
			try
			{
				field = ConfigNodeUtil.ParseValue<RadiationField>(node, "field", RadiationField.UNDEFINED);
				targetBody = ConfigNodeUtil.ParseValue<CelestialBody>(node, "targetBody", (CelestialBody)null);
			}
			finally
			{
				ParameterDelegate<Vessel>.OnDelegateContainerLoad(node);
			}
		}

		protected override void OnRegister()
		{
			base.OnRegister();
			RadiationFieldTracker.AddListener(RunCheck);
		}

		protected override void OnUnregister()
		{
			base.OnUnregister();
			RadiationFieldTracker.RemoveListener(RunCheck);
		}

		private void RunCheck(Vessel v, bool inner_belt, bool outer_belt, bool magnetosphere)
		{
			var bd = KerbalismContracts.Instance.BodyData(targetBody);

			switch(field)
			{
				case RadiationField.INNER_BELT:
					SetState(bd.inner_visible | !bd.has_inner ? ParameterState.Complete : ParameterState.Incomplete);
					break;
				case RadiationField.OUTER_BELT:
					SetState(bd.outer_visible | !bd.has_outer ? ParameterState.Complete : ParameterState.Incomplete);
					break;
				case RadiationField.MAGNETOPAUSE:
					SetState(bd.pause_visible | !bd.has_pause ? ParameterState.Complete : ParameterState.Incomplete);
					break;
				case RadiationField.ANY:
					bool hasNone = !bd.has_inner && !bd.has_outer && !bd.has_pause;
					SetState(hasNone | bd.inner_visible | bd.outer_visible | bd.pause_visible ? ParameterState.Complete : ParameterState.Incomplete);
					break;
			}
		}
	}

	public class ShowRadiationFieldFactory : BehaviourFactory
	{
		protected RadiationField field;
		protected bool set_visible = false;
		protected bool require_completed = false;

		public override bool Load(ConfigNode configNode)
		{
			bool valid = base.Load(configNode);

			valid &= ConfigNodeUtil.ParseValue<RadiationField>(configNode, "field", x => field = x, this, RadiationField.UNDEFINED, ValidateField);
			valid &= ConfigNodeUtil.ParseValue<bool>(configNode, "set_visible", x => set_visible = x, this, true);
			valid &= ConfigNodeUtil.ParseValue<bool>(configNode, "require_completed", x => require_completed = x, this, false);

			return valid;
		}

		private bool ValidateField(RadiationField f)
		{
			if (f == RadiationField.UNDEFINED)
			{
				LoggingUtil.LogError(this, "Missing field. You must specify field = INNER_BELT, OUTER_BELT, MAGNETOPAUSE or ANY.");
				return false;
			}
			return true;
		}

		public override ContractBehaviour Generate(ConfiguredContract contract)
		{
			return new ShowRadiationField(targetBody, field, set_visible, require_completed);
		}
	}

	public class ShowRadiationField : ContractBehaviour
	{
		protected CelestialBody targetBody;
		protected RadiationField field;
		protected bool set_visible;
		protected bool require_completed;

		public ShowRadiationField(): base() {}

		public ShowRadiationField(CelestialBody targetBody, RadiationField field, bool set_visible, bool require_completed)
		{
			this.targetBody = targetBody;
			this.field = field;
			this.set_visible = set_visible;
			this.require_completed = require_completed;
		}

		protected override void OnLoad(ConfigNode configNode)
		{
			base.OnLoad(configNode);

			targetBody = ConfigNodeUtil.ParseValue<CelestialBody>(configNode, "targetBody");
			field = ConfigNodeUtil.ParseValue<RadiationField>(configNode, "field", RadiationField.UNDEFINED);
			set_visible = ConfigNodeUtil.ParseValue<bool>(configNode, "set_visible", true);
			require_completed = ConfigNodeUtil.ParseValue<bool>(configNode, "require_completed", true);
		}

		protected override void OnSave(ConfigNode configNode)
		{
			base.OnSave(configNode);

			configNode.AddValue("targetBody", targetBody.name);
			configNode.AddValue("field", field);
			configNode.AddValue("set_visible", set_visible);
			configNode.AddValue("require_completed", require_completed);
		}

		protected override void OnCompleted()
		{
			base.OnCompleted();
			DoShow();
		}

		protected override void OnParameterStateChange(ContractParameter param)
		{
			base.OnParameterStateChange(param);

			if (require_completed || param.State != ParameterState.Complete)
				return;

			var matchingParameter = GetMatchingParameter(param);
			if (matchingParameter == null)
				return;

			if (matchingParameter.State == ParameterState.Complete)
				DoShow();
		}

		protected RadiationFieldParameter GetMatchingParameter(ContractParameter param) {
			var radiationFieldParameter = param as RadiationFieldParameter;
			if (radiationFieldParameter == null)
			{
				foreach(ContractParameter child in param.GetChildren())
				{
					var result = GetMatchingParameter(child);
					if (result != null) return result;
				}	
			}
			else if (radiationFieldParameter.field == field || field == RadiationField.ANY)
			{
				return radiationFieldParameter;
			}
			return null;
		}

		protected void DoShow()
		{
			if (targetBody == null) return;

			switch (field)
			{
				case RadiationField.INNER_BELT:
					KerbalismContracts.SetInnerBeltVisible(targetBody, set_visible);
					break;

				case RadiationField.OUTER_BELT:
					KerbalismContracts.SetOuterBeltVisible(targetBody, set_visible);
					break;

				case RadiationField.MAGNETOPAUSE:
					KerbalismContracts.SetMagnetopauseVisible(targetBody, set_visible);
					break;

				case RadiationField.ANY:
					KerbalismContracts.SetInnerBeltVisible(targetBody, set_visible);
					KerbalismContracts.SetOuterBeltVisible(targetBody, set_visible);
					KerbalismContracts.SetMagnetopauseVisible(targetBody, set_visible);
					break;
			}
		}
	}
}
