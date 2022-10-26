using System;

namespace UnityEngine.XR.Hands.ProviderImplementation
{
    /// <summary>
    /// Utility methods for a provider to a <see cref="XRHandSubsystem"/>.
    /// </summary>
    public static class XRHandProviderUtility
    {
        /// <summary>
        /// Create a fully configurable joint with at least a pose in hand
        /// space, tracking state, and ID.
        /// </summary>
        /// <param name="trackingState">
        /// The tracking state flags associated with this joint, representing
        /// which fields of the <see cref="XRHandJoint"/> are valid.
        /// </param>
        /// <param name="id">
        /// The <see cref="XRHandJointID"/> of the joint. When filling out an
        /// element of an array of <see cref="XRHandJoint"/>, the index must
        /// be converted with <see cref="XRHandJointIDUtility.FromIndex"/>.
        /// </param>
        /// <param name="pose">
        /// The pose of the joint in session space, relative to the
        /// <c>XROrigin</c>.
        /// </param>
        /// <param name="radius">
        /// The radius of the joint. The default value is <c>0f</c>.
        /// </param>
        /// <param name="linearVelocity">
        /// The linear velocity of the joint in hand space (relative to the
        /// <c>XROrigin</c>). The default value is <c>Vector3.zero</c>.
        /// </param>
        /// <param name="angularVelocity">
        /// The angular velocity of the joint in hand space (relative to the
        /// <c>XROrigin</c>). The default value is <c>Vector3.zero</c>.
        /// </param>
        /// <returns>
        /// An <see cref="XRHandJoint"/> with the given pose and other supplied
        /// data.
        /// </returns>
        public static XRHandJoint CreateJoint(
            XRHandJointTrackingState trackingState,
            XRHandJointID id,
            Pose pose,
            float radius = 0f,
            Vector3 linearVelocity = new Vector3(),
            Vector3 angularVelocity = new Vector3())
        {
            return new XRHandJoint
            {
                m_TrackingState = trackingState,
                m_Id = id,
                m_Pose = pose,
                m_Radius = radius,
                m_LinearVelocity = linearVelocity,
                m_AngularVelocity = angularVelocity,
            };
        }

        /// <summary>
        /// Use this with your provider (if hand-tracking is enabled in your
        /// build settings) to have the subsystem automatically update. Users
        /// can subscribe to <see cref="XRHandSubsystem.handsUpdated"/> to
        /// respond to updates instead of waiting until a following update step.
        /// Makes use of <c>UnityEngine.LowLevel.PlayerLoop</c> to insert this
        /// automatic update at the beginning of the <c>EarlyUpdate.XRUpdate</c>
        /// loop.
        /// </summary>
        public class SubsystemUpdater
        {
            /// <summary>
            /// Initializes and returns an instance of <see cref="SubsystemUpdater"/>.
            /// You should create this in your plug-in once the <see cref="XRHandSubsystem"/>
            /// is created.
            /// </summary>
            /// <param name="subsystem">
            /// The <see cref="XRHandSubsystem"/> to update automatically.
            /// </param>
            public SubsystemUpdater(XRHandSubsystem subsystem) => m_Subsystem = subsystem;

            /// <summary>
            /// Starts the automatic updating of the <see cref="XRHandSubsystem"/>.
            /// You should call this from your plug-in when calling <c>Start</c>
            /// on the subsystem.
            /// </summary>
            public void Start()
            {
                int earlyUpdateStepIndex = -1;
                var playerLoop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
                for (int subSystemIndex = 0; subSystemIndex < playerLoop.subSystemList.Length; ++subSystemIndex)
                {
                    if (playerLoop.subSystemList[subSystemIndex].type == typeof(UnityEngine.PlayerLoop.EarlyUpdate))
                    {
                        earlyUpdateStepIndex = subSystemIndex;
                        break;
                    }
                }

                if (earlyUpdateStepIndex < 0)
                {
                    Debug.LogError("Couldn't find PlayerLoop.EarlyUpdate step - XRHandSubsystem will not automatically update!");
                    return;
                }

                int xrUpdateIndex = -1;
                for (int subSystemIndex = 0; subSystemIndex < playerLoop.subSystemList[earlyUpdateStepIndex].subSystemList.Length; ++subSystemIndex)
                {
                    if (playerLoop.subSystemList[earlyUpdateStepIndex].subSystemList[subSystemIndex].type == typeof(UnityEngine.PlayerLoop.EarlyUpdate.XRUpdate))
                    {
                        xrUpdateIndex = subSystemIndex;
                        break;
                    }
                }

                if (xrUpdateIndex < 0)
                {
                    Debug.LogError("Couldn't find PlayerLoop.EarlyUpdate.XRUpdate step - XRHandSubsystem will not automatically update!");
                    return;
                }

                var systems = playerLoop.subSystemList[earlyUpdateStepIndex].subSystemList[xrUpdateIndex].subSystemList;
                if (systems != null)
                {
                    foreach (var system in systems)
                    {
                        if (system.type == typeof(XRHandSubsystemPlayerLoopRunnerUpdateSystem))
                            return;
                    }
                }

                var updatedSystems = new UnityEngine.LowLevel.PlayerLoopSystem[systems != null ? systems.Length + 1 : 1];
                if (systems != null)
                    Array.Copy(systems, 0, updatedSystems, 1, systems.Length);

                updatedSystems[0] = new UnityEngine.LowLevel.PlayerLoopSystem
                {
                    type = typeof(XRHandSubsystemPlayerLoopRunnerUpdateSystem),
                    updateDelegate = () => OnUpdate()
                };

                Application.onBeforeRender += OnBeforeRender;
                playerLoop.subSystemList[earlyUpdateStepIndex].subSystemList[xrUpdateIndex].subSystemList = updatedSystems;
                UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(playerLoop);
            }

            /// <summary>
            /// Stops the automatic updating of the <see cref="XRHandSubsystem"/>.
            /// You should call this from your plug-in when calling <c>Stop</c>
            /// on the subsystem.
            /// </summary>
            public void Stop()
            {
                Application.onBeforeRender -= OnBeforeRender;

                var playerLoop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();

                int earlyUpdateStepIndex = -1;
                for (int subSystemIndex = 0; subSystemIndex < playerLoop.subSystemList.Length; ++subSystemIndex)
                {
                    if (playerLoop.subSystemList[subSystemIndex].type == typeof(UnityEngine.PlayerLoop.EarlyUpdate))
                    {
                        earlyUpdateStepIndex = subSystemIndex;
                        break;
                    }
                }

                if (earlyUpdateStepIndex < 0)
                    return;

                int xrUpdateIndex = -1;
                for (int subSystemIndex = 0; subSystemIndex < playerLoop.subSystemList[earlyUpdateStepIndex].subSystemList.Length; ++subSystemIndex)
                {
                    if (playerLoop.subSystemList[earlyUpdateStepIndex].subSystemList[subSystemIndex].type == typeof(UnityEngine.PlayerLoop.EarlyUpdate.XRUpdate))
                    {
                        xrUpdateIndex = subSystemIndex;
                        break;
                    }
                }

                if (xrUpdateIndex < 0)
                    return;

                int indexOfOurType = -1;
                var systems = playerLoop.subSystemList[earlyUpdateStepIndex].subSystemList[xrUpdateIndex].subSystemList;
                if (systems == null)
                    return;

                for (int systemIndex = 0; systemIndex < systems.Length; ++systemIndex)
                {
                    if (systems[systemIndex].type == typeof(XRHandSubsystemPlayerLoopRunnerUpdateSystem))
                    {
                        indexOfOurType = systemIndex;
                        break;
                    }
                }

                if (indexOfOurType < 0)
                    return;

                if (indexOfOurType == 0 && systems.Length == 1)
                    systems = null;
                else if (indexOfOurType < systems.Length - 1)
                    Array.Copy(systems, indexOfOurType + 1, systems, indexOfOurType, systems.Length - indexOfOurType - 1);

                if (systems != null)
                    Array.Resize(ref systems, systems.Length - 1);

                playerLoop.subSystemList[earlyUpdateStepIndex].subSystemList[xrUpdateIndex].subSystemList = systems;
                UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(playerLoop);
            }

            /// <summary>
            /// Destroys the automatic updating of the <see cref="XRHandSubsystem"/>.
            /// You should call this from your plug-in when calling <c>Destroy</c>
            /// on the subsystem.
            /// </summary>
            public void Destroy()
            {
                Stop();
                m_Subsystem = null;
            }

            void OnUpdate() => m_Subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            void OnBeforeRender() => m_Subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.BeforeRender);

            XRHandSubsystem m_Subsystem;

            struct XRHandSubsystemPlayerLoopRunnerUpdateSystem
            {
            }
        }
    }
}
