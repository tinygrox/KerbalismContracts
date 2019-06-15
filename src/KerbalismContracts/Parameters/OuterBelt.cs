﻿using System;
using System.Collections.Generic;
using Contracts;
using KSP.Localization;
using ContractConfigurator;
using ContractConfigurator.Parameters;

namespace Kerbalism.Contracts
{
	public class OuterBeltFactory : ParameterFactory
	{
		public override ContractParameter Generate(Contract contract)
		{
			return new OuterBelt();
		}
	}

	public class OuterBelt : VesselParameter
	{
		protected override string GetParameterTitle()
		{
			if (!string.IsNullOrEmpty(title)) return title;
			return "Be in the outer radiation belt";
		}

		protected override bool VesselMeetsCondition(Vessel vessel)
		{
			return KERBALISM.API.OuterBelt(vessel);
		}
	}

}
