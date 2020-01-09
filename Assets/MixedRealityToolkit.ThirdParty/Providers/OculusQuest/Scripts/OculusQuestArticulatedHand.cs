using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HoloLab.MixedReality.Toolkit.OculusQuest.Input
{
    [MixedRealityController(SupportedControllerType.ArticulatedHand, new[] { Handedness.Left, Handedness.Right })]
    public class OculusQuestArticulatedHand : BaseController, IMixedRealityHand
    {
        private OVRHand hand = null;
        private OVRSkeleton skeleton = null;

        private Dictionary<TrackedHandJoint, MixedRealityPose> jointPose = new Dictionary<TrackedHandJoint, MixedRealityPose>();

        public OculusQuestArticulatedHand(
            TrackingState trackingState,
            Handedness controllerHandedness,
            IMixedRealityInputSource inputSource = null,
            MixedRealityInteractionMapping[] interactions = null) : base(trackingState, controllerHandedness, inputSource, interactions)
        {
            var rigs = GameObject.FindObjectOfType<OVRCameraRig>();
            var hands = rigs.GetComponentsInChildren<OVRHand>();
            foreach (var item in hands)
            {
                switch (((OVRSkeleton.IOVRSkeletonDataProvider)item).GetSkeletonType())
                {
                    case OVRSkeleton.SkeletonType.HandLeft:
                        if (controllerHandedness == Handedness.Left)
                        {
                            hand = item;
                            skeleton = item?.GetComponent<OVRSkeleton>();
                        }
                        break;
                    case OVRSkeleton.SkeletonType.HandRight:
                        if (controllerHandedness == Handedness.Right)
                        {
                            hand = item;
                            skeleton = item?.GetComponent<OVRSkeleton>();
                        }
                        break;
                }
            }

            //var anchor = GameObject.Find((controllerHandedness == Handedness.Left) ? "LeftHandAnchor" : "RightHandAnchor");
            //hand = anchor?.GetComponentInChildren<OVRHand>();
            //skeleton = anchor?.GetComponentInChildren<OVRSkeleton>();
        }

        public override MixedRealityInteractionMapping[] DefaultInteractions => new[]
       {
             new MixedRealityInteractionMapping(0, "Spatial Pointer", AxisType.SixDof, DeviceInputType.SpatialPointer, MixedRealityInputAction.None),
             new MixedRealityInteractionMapping(1, "Spatial Grip", AxisType.SixDof, DeviceInputType.SpatialGrip, MixedRealityInputAction.None),
             new MixedRealityInteractionMapping(2, "Select", AxisType.Digital, DeviceInputType.Select, MixedRealityInputAction.None),
             new MixedRealityInteractionMapping(3, "Grab", AxisType.SingleAxis, DeviceInputType.TriggerPress, MixedRealityInputAction.None),
             new MixedRealityInteractionMapping(4, "Index Finger Pose", AxisType.SixDof, DeviceInputType.IndexFinger, MixedRealityInputAction.None)
         };

        public override MixedRealityInteractionMapping[] DefaultLeftHandedInteractions => DefaultInteractions;

        public override MixedRealityInteractionMapping[] DefaultRightHandedInteractions => DefaultInteractions;

        public override void SetupDefaultInteractions(Handedness controllerHandedness)
        {
            AssignControllerMappings(DefaultInteractions);
        }

        public bool TryGetJoint(TrackedHandJoint joint, out MixedRealityPose pose)
        {
            return jointPose.TryGetValue(joint, out pose);
        }

        public override bool IsInPointingPose => (hand != null) ? hand.IsPointerPoseValid : false;

        public void UpdateController()
        {
            if (!Enabled) { return; }

            // hand pose
            var lastState = TrackingState;
            TrackingState = (hand.IsTracked) ? TrackingState.Tracked : TrackingState.NotTracked;
            if (lastState != TrackingState)
            {
                CoreServices.InputSystem?.RaiseSourceTrackingStateChanged(InputSource, this, TrackingState);
            }
            if (TrackingState == TrackingState.Tracked)
            {
                var pose = new MixedRealityPose();
                pose.Position = MixedRealityPlayspace.TransformPoint(hand.transform.position);
                pose.Rotation = MixedRealityPlayspace.Rotation * hand.transform.rotation;
                CoreServices.InputSystem?.RaiseSourcePoseChanged(InputSource, this, pose);
            }

            // hand interaction
            if (Interactions == null)
            {
                Debug.LogError($"No interaction configuration for Oculus Quest Hand {ControllerHandedness} Source");
                Enabled = false;
            }
            if (TrackingState == TrackingState.Tracked)
            {
                for (int i = 0; i < Interactions?.Length; i++)
                {
                    var interaction = Interactions[i];
                    switch (interaction.InputType)
                    {
                        case DeviceInputType.None:
                            break;
                        case DeviceInputType.SpatialPointer:
                            // hand pointer
                            var pointer = new MixedRealityPose();
                            pointer.Position = MixedRealityPlayspace.TransformPoint(hand.PointerPose.position);
                            pointer.Rotation = MixedRealityPlayspace.Rotation * hand.PointerPose.rotation;
                            interaction.PoseData = pointer;
                            if (interaction.Changed)
                            {
                                CoreServices.InputSystem?.RaisePoseInputChanged(InputSource, ControllerHandedness, interaction.MixedRealityInputAction, pointer);
                            }
                            break;
                        case DeviceInputType.SpatialGrip:
                            if (interaction.AxisType == AxisType.SixDof)
                            {
                                var grip = new MixedRealityPose();
                                grip.Position = MixedRealityPlayspace.TransformPoint(hand.transform.position);
                                grip.Rotation = MixedRealityPlayspace.Rotation * hand.transform.rotation;
                                interaction.PoseData = grip;
                                if (interaction.Changed)
                                {
                                    CoreServices.InputSystem?.RaisePoseInputChanged(InputSource, ControllerHandedness, interaction.MixedRealityInputAction, grip);
                                }
                            }
                            break;
                        case DeviceInputType.Select:
                        case DeviceInputType.TriggerPress:
                            interaction.BoolData = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
                            if (interaction.Changed)
                            {
                                if (interaction.BoolData)
                                {
                                    CoreServices.InputSystem?.RaiseOnInputDown(InputSource, ControllerHandedness, interaction.MixedRealityInputAction);
                                }
                                else
                                {
                                    CoreServices.InputSystem?.RaiseOnInputUp(InputSource, ControllerHandedness, interaction.MixedRealityInputAction);
                                }
                            }
                            break;
                        case DeviceInputType.IndexFinger:
                            if (jointPose.ContainsKey(TrackedHandJoint.IndexTip))
                            {
                                var indexFinger = jointPose[TrackedHandJoint.IndexTip];
                                interaction.PoseData = indexFinger;
                                if (interaction.Changed)
                                {
                                    CoreServices.InputSystem?.RaisePoseInputChanged(InputSource, ControllerHandedness, interaction.MixedRealityInputAction, indexFinger);
                                }
                            }
                            break;
                    }
                }
            }

            // hand joint
            if (TrackingState == TrackingState.Tracked)
            {
                for (int i = 0; i < skeleton.Bones.Count; i++)
                {
                    var bones = skeleton.Bones[i];
                    var handJoint = convertBoneIdToTrackedHandJoint(bones.Id);
                    var position = MixedRealityPlayspace.TransformPoint(bones.Transform.position);
                    var rotation = MixedRealityPlayspace.Rotation * bones.Transform.rotation;
                    if (jointPose.ContainsKey(handJoint))
                    {
                        jointPose[handJoint] = new MixedRealityPose(position, rotation);
                    }
                    else
                    {
                        jointPose.Add(handJoint, new MixedRealityPose(position, rotation));
                    }
                }
                CoreServices.InputSystem?.RaiseHandJointsUpdated(InputSource, ControllerHandedness, jointPose);
            }
        }

        private TrackedHandJoint convertBoneIdToTrackedHandJoint(OVRSkeleton.BoneId boneId)
        {
            switch (boneId)
            {
                case OVRSkeleton.BoneId.Hand_WristRoot: return TrackedHandJoint.Wrist;
                case OVRSkeleton.BoneId.Hand_ForearmStub: return TrackedHandJoint.Palm;
                case OVRSkeleton.BoneId.Hand_Thumb0: return TrackedHandJoint.ThumbMetacarpalJoint;
                case OVRSkeleton.BoneId.Hand_Thumb1: return TrackedHandJoint.ThumbProximalJoint;
                case OVRSkeleton.BoneId.Hand_Thumb2: return TrackedHandJoint.ThumbDistalJoint;
                case OVRSkeleton.BoneId.Hand_Thumb3: return TrackedHandJoint.ThumbTip;
                case OVRSkeleton.BoneId.Hand_Index1: return TrackedHandJoint.IndexKnuckle;
                case OVRSkeleton.BoneId.Hand_Index2: return TrackedHandJoint.IndexMiddleJoint;
                case OVRSkeleton.BoneId.Hand_Index3: return TrackedHandJoint.IndexDistalJoint;
                case OVRSkeleton.BoneId.Hand_Middle1: return TrackedHandJoint.MiddleKnuckle;
                case OVRSkeleton.BoneId.Hand_Middle2: return TrackedHandJoint.MiddleMiddleJoint;
                case OVRSkeleton.BoneId.Hand_Middle3: return TrackedHandJoint.MiddleDistalJoint;
                case OVRSkeleton.BoneId.Hand_Ring1: return TrackedHandJoint.RingKnuckle;
                case OVRSkeleton.BoneId.Hand_Ring2: return TrackedHandJoint.RingMiddleJoint;
                case OVRSkeleton.BoneId.Hand_Ring3: return TrackedHandJoint.RingDistalJoint;
                case OVRSkeleton.BoneId.Hand_Pinky0: return TrackedHandJoint.PinkyMetacarpal;
                case OVRSkeleton.BoneId.Hand_Pinky1: return TrackedHandJoint.PinkyKnuckle;
                case OVRSkeleton.BoneId.Hand_Pinky2: return TrackedHandJoint.PinkyMiddleJoint;
                case OVRSkeleton.BoneId.Hand_Pinky3: return TrackedHandJoint.PinkyDistalJoint;
                case OVRSkeleton.BoneId.Hand_ThumbTip: return TrackedHandJoint.ThumbTip;
                case OVRSkeleton.BoneId.Hand_IndexTip: return TrackedHandJoint.IndexTip;
                case OVRSkeleton.BoneId.Hand_MiddleTip: return TrackedHandJoint.MiddleTip;
                case OVRSkeleton.BoneId.Hand_RingTip: return TrackedHandJoint.RingTip;
                case OVRSkeleton.BoneId.Hand_PinkyTip: return TrackedHandJoint.PinkyTip;
                default: return TrackedHandJoint.None;
            }
        }
    }
}
