//Sytems
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
//Sandboxs
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Components;
using Sandbox.Game.Entities;
using Sandbox.ModAPI.Interfaces.Terminal;
using Sandbox.ModAPI;
using Sandbox.Definitions;
//Vrage
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using VRage.Game.ModAPI.Network;
using VRage.Sync;

namespace Blues_Thruster_Particles
{
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), false)]


	public class Thrusters : MyGameLogicComponent
	{
		public static Thrusters Instance;

		public Guid guid = new Guid("C8DAD855-2F60-401A-A5D4-09D0C6D14BD6");

		public static bool IsClient => !(IsServer && IsDedicated);
		public static bool IsDedicated => MyAPIGateway.Utilities.IsDedicated;
		public static bool IsServer => MyAPIGateway.Multiplayer.IsServer;
		public static bool IsActive => MyAPIGateway.Multiplayer.MultiplayerActive;

		public MySync<bool, SyncDirection.BothWays> requiresUpdate;

		
		string particleeffect = "";

		private string BlockSizeAdjuster = "";
		private float ParticleSizeAdjuster;
		//My Thrusters 
		private IMyThrust CoreBlock;
		public MyThrust MyCoreBlock;
		public MyThrustDefinition MyCoreBlockDefinition;
		IMyTerminalBlock terminalBlock;

		private MatrixD ParticleMatrix = MatrixD.Identity;
		private Vector3D ParticlePosition = Vector3D.Zero;
		private VRage.Game.MyParticleEffect ParticleEmitter;
		private bool IsStopped=false;

		public string ParticleEffectToGenerate;
		public Vector4 FlameColor;


		public override void Init(MyObjectBuilder_EntityBase objectBuilder)
		{

			Instance = this;
			//Update Every Frame
			NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
			//Grab MyThruster
			CoreBlock = Entity as IMyThrust;
			MyCoreBlock = CoreBlock as MyThrust;
			MyCoreBlockDefinition = MyCoreBlock.BlockDefinition;
			terminalBlock = Entity as IMyTerminalBlock;

			
			ParticleEffectToGenerate = "";


			//Adapt for block size
			string SubtypeId = CoreBlock.BlockDefinition.SubtypeId;
			if ((SubtypeId.Contains("LargeBlock") && SubtypeId.Contains("BlockLarge")) || SubtypeId.Contains("LG_FusionDrive") || SubtypeId.Contains("LG_HydroThrusterL"))
			{
				BlockSizeAdjuster = " LgLb";
				ParticleSizeAdjuster = 2.8f;
			}

			if ((SubtypeId.Contains("LargeBlock") && SubtypeId.Contains("BlockSmall")) || SubtypeId.Contains("SG_FusionDrive") || SubtypeId.Contains("LG_HydroThrusterS"))
			{
				BlockSizeAdjuster = " LgSb";
				ParticleSizeAdjuster = 1f;
			}

			if ((SubtypeId.Contains("SmallBlock") && SubtypeId.Contains("BlockLarge")) || SubtypeId.Contains("SG_HydroThrusterL"))
			{
				BlockSizeAdjuster = " SgLb";
				ParticleSizeAdjuster = 0.5f;
			}

			if ((SubtypeId.Contains("SmallBlock") && SubtypeId.Contains("BlockSmall")) || SubtypeId.Contains("SG_HydroThrusterS"))
			{
				BlockSizeAdjuster = " SgSb";
				ParticleSizeAdjuster = 0.1f;
			}
			if(SubtypeId.Contains("STR"))
			{
				//Small Grid
				if(SubtypeId.Contains("50")&& !SubtypeId.Contains("550")) {ParticleSizeAdjuster = 1f;BlockSizeAdjuster = " 50";}
				if(SubtypeId.Contains("100")){ParticleSizeAdjuster = 1f;BlockSizeAdjuster = " 100";}
				if(SubtypeId.Contains("200")){ParticleSizeAdjuster = 1f;BlockSizeAdjuster = " 200";}
				if(SubtypeId.Contains("300")){ParticleSizeAdjuster = 1f;BlockSizeAdjuster = " 300";}
				//LargeGrid
				if(SubtypeId.Contains("350")){ParticleSizeAdjuster = 1f;BlockSizeAdjuster = " 350";}
				if(SubtypeId.Contains("400")){ParticleSizeAdjuster = 1f;BlockSizeAdjuster = " 400";}
				if(SubtypeId.Contains("500")){ParticleSizeAdjuster = 1f;BlockSizeAdjuster = " 500";}
				if(SubtypeId.Contains("550")){ParticleSizeAdjuster = 1f;BlockSizeAdjuster = " 550";}
				if(SubtypeId.Contains("600")){ParticleSizeAdjuster = 1f;BlockSizeAdjuster = " 600";}
			}
			if (IsDedicated){return;}
			LoadCustomData();
			UpdateCustomData();
			requiresUpdate.ValidateAndSet(true);
			
					
		}
		//UpdateAfterSimulation

		public override void UpdateAfterSimulation()
		{
			/*if (MyCoreBlockDefinition.FuelConverter.FuelId != HydrogenId)
				return;*/		

			if (IsDedicated){return;}

			CustomControls.AddControls(ModContext);
			
			if (requiresUpdate.Value)
			{
				try
				{
					requiresUpdate.ValidateAndSet(false);
					if(ParticleEmitter != null)
					{
						MyCoreBlockDefinition.FlameFullColor = Vector4.Zero;
						MyCoreBlockDefinition.FlameIdleColor = Vector4.Zero;
					}
					else
					{
						MyCoreBlockDefinition.FlameFullColor = FlameColor;
						MyCoreBlockDefinition.FlameIdleColor = FlameColor;
					}
					(MyCoreBlock.Render).UpdateFlameAnimatorData();
				}
				catch{MyLog.Default.WriteLine("Un-Able to ajust thruster flame");}
			}
			
			//MyAPIGateway.Parallel.Start(delegate{});
			//if(CoreBlock.BlockDefinition.SubtypeId.Contains("STR_DISABLED")){particleSize=1f;}
			//Create and Maintain
			if(ParticleEffectToGenerate == "Vanilla"||ParticleEffectToGenerate == "")
			{
				if(ParticleEmitter != null)
				{
					ParticleEmitter.UserLifeMultiplier=0f;
					ParticleEmitter.UserScale=0f;
					ParticleEmitter.Stop();
					ParticleEmitter.Close();
					ParticleEmitter = null;
					requiresUpdate.ValidateAndSet(true);
				}
				return;
			}
			string ParticleToCreate;
			float ParticleRadius = 1f;
			try 
			{ 
				ParticleToCreate = Globals.ParticleEffectsList[ParticleEffectToGenerate] + BlockSizeAdjuster; 
			}
			catch 
			{ 
				ParticleToCreate = "Blueshift" + BlockSizeAdjuster; 
			}		
			//Start particle effects
			float ThrusterOutput = CoreBlock.CurrentThrust / CoreBlock.MaxThrust;
			//Create if None Exist
			if (ParticleEmitter == null)
			{
				ParticleMatrix = CoreBlock.WorldMatrix;
				ParticlePosition = ParticleMatrix.Translation;
				if(MyParticlesManager.TryCreateParticleEffect(ParticleToCreate, ref ParticleMatrix, ref ParticlePosition, uint.MaxValue, out ParticleEmitter))
				{
					particleeffect=ParticleEffectToGenerate;
					ParticleEmitter.UserRadiusMultiplier = ParticleRadius;
					ParticleEmitter.UserLifeMultiplier = ThrusterOutput;
					ParticleEmitter.UserScale = ParticleSizeAdjuster;
					ParticleEmitter.UserVelocityMultiplier=1f;
					//ParticleEmitter.UserBirthMultiplier=1f;
					ParticleEmitter.UserColorIntensityMultiplier=1f;
					ParticleEmitter.UserColorMultiplier=FlameColor;
					ParticleEmitter.StopLights();
					//ParticleEmitter.UserFadeMultiplier=1f;
					ParticleEmitter.Play();
					requiresUpdate.ValidateAndSet(true);
				}
			}
			//Adjust Exising Effect
			else
			{
				if (particleeffect != ParticleEffectToGenerate || !(CoreBlock.Enabled && CoreBlock.IsFunctional))
				{
					ParticleEmitter.UserScale=0f;
					ParticleEmitter.UserLifeMultiplier=0f;
					ParticleEmitter.Stop();
					ParticleEmitter.Close();
					ParticleEmitter=null;
					return;
				}
				ParticleEmitter.WorldMatrix = CoreBlock.WorldMatrix;
				ParticleEmitter.UserRadiusMultiplier = ParticleRadius;
				ParticleEmitter.UserLifeMultiplier = ThrusterOutput;
				if(ThrusterOutput> 0.049){ParticleEmitter.UserScale = ParticleSizeAdjuster;}else{ParticleEmitter.UserScale=0f;}
				ParticleEmitter.UserVelocityMultiplier=1f;
				//ParticleEmitter.UserBirthMultiplier=1f;
				ParticleEmitter.UserColorIntensityMultiplier=1f;
				ParticleEmitter.UserColorMultiplier=FlameColor;
				//ParticleEmitter.UserFadeMultiplier=1f;
				ParticleEmitter.Play();
			}


		}
		/*public override void UpdateAfterSimulation10()
		{

		}*/
		public static Thrusters GetInstance()
		{
			return Instance;
		}

		public void UpdateCustomData()
		{
			requiresUpdate.ValidateAndSet(true);
			if(ParticleEffectToGenerate == ""){ParticleEffectToGenerate="Vanilla";}
			CoreBlock.CustomData = $"{ParticleEffectToGenerate}:{FlameColor.X}:{FlameColor.Y}:{FlameColor.Z}:{FlameColor.W}";
		}

		public void LoadCustomData()
		{
			var parts = CoreBlock.CustomData.Split(':');
			if (parts.Length != 5)
			{
				ParticleEffectToGenerate = "";
				if (MyCoreBlockDefinition.FuelConverter.FuelId == Globals.HydrogenId)
					{FlameColor = new Vector4(1f, 0.7f, 0.0f, 0.5f);}
				else if(CoreBlock.BlockDefinition.SubtypeId.ToLower().Contains("atmo"))
					{FlameColor = new Vector4(1f, 1f, 1f, 0.5f);}
				else
					{FlameColor = new Vector4(0.60f, 1.40f, 2.55f, 0.5f);}
				//FlameColor = Color.White; Don't mess with what doesnt need messsed with :)
			}
			else
			{
				try
				{
					var vector = new Vector4(float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]), float.Parse(parts[4]));
					ParticleEffectToGenerate = parts[0];
					if(ParticleEffectToGenerate == ""){ParticleEffectToGenerate="Vanilla";}
					FlameColor = vector;
				}
				catch (Exception x)
				{
					ParticleEffectToGenerate = "Vanilla";
					FlameColor = Color.Red;//I wanna know when shit goes wrong
				}
			}
			UpdateCustomData();//So Custom data will be updated after all effects
		}

		public override void Close()
		{
			if (ParticleEmitter != null)
			{
				ParticleEmitter.Close();				
			}
			Instance = null;

		}

	}
}