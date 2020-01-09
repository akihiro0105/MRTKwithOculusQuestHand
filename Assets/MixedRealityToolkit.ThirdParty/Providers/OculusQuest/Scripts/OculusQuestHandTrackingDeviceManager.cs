using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HoloLab.MixedReality.Toolkit.OculusQuest.Input
{
    [MixedRealityDataProvider(typeof(IMixedRealityInputSystem),
        SupportedPlatforms.Android|SupportedPlatforms.WindowsEditor|SupportedPlatforms.MacEditor|SupportedPlatforms.LinuxEditor,
        "Oculus Quest Hand Device Manager")]
    public class OculusQuestHandTrackingDeviceManager : BaseInputDeviceManager, IMixedRealityCapabilityCheck
    {
        private OculusQuestArticulatedHand leftHand = null;
        private OculusQuestArticulatedHand rightHand = null;

        public OculusQuestHandTrackingDeviceManager(
            IMixedRealityServiceRegistrar registrar,
           IMixedRealityInputSystem inputSystem,
           string name = null,
           uint priority = DefaultPriority,
           BaseMixedRealityProfile profile = null) : base(inputSystem, name, priority, profile) { }

        public OculusQuestHandTrackingDeviceManager(
            IMixedRealityInputSystem inputSystem,
            string name = null,
            uint priority = DefaultPriority,
            BaseMixedRealityProfile profile = null) : base(inputSystem, name, priority, profile) { }

        #region IMixedRealityCapabilityCheck Implementation
        public bool CheckCapability(MixedRealityCapability capability)
        {
            return (capability == MixedRealityCapability.ArticulatedHand);
        }
        #endregion IMixedRealityCapabilityCheck Implementation

        public override IMixedRealityController[] GetActiveControllers()
        {
            var list = new List<IMixedRealityController>();
            if (leftHand != null) list.Add(leftHand);
            if (rightHand != null) list.Add(rightHand);
            return list.ToArray();
        }

        public override void Update()
        {
            base.Update();

            if (leftHand == null) leftHand = createArticulatedHand(Handedness.Left);
            leftHand?.UpdateController();

            if (rightHand == null) rightHand = createArticulatedHand(Handedness.Right);
            rightHand?.UpdateController();
        }

        private OculusQuestArticulatedHand createArticulatedHand(Handedness handedness)
        {
            var inputSystem = Service as IMixedRealityInputSystem;
            var pointers = RequestPointers(SupportedControllerType.ArticulatedHand, handedness);
            var inputSource = inputSystem?.RequestNewGenericInputSource($"Oculus Quest Hand Controller {handedness.ToString()}", pointers, InputSourceType.Hand);
            var hand = new OculusQuestArticulatedHand(TrackingState.NotTracked, handedness, inputSource);
            hand.SetupConfiguration(typeof(OculusQuestArticulatedHand), InputSourceType.Hand);
            for (int i = 0; i < hand.InputSource?.Pointers?.Length; i++)
            {
                hand.InputSource.Pointers[i].Controller = hand;
            }
            inputSystem.RaiseSourceDetected(hand.InputSource, hand);
            return hand;
        }
    }
}
