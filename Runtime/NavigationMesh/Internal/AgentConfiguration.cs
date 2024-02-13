// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Utilities;

namespace Niantic.Lightship.AR.NavigationMesh
{
    [PublicAPI]
    public readonly struct AgentConfiguration
    {
        /// The maximum distance an agent can jump in meters.
        public readonly float JumpDistance;

        /// Determines the cost of jumping.
        /// @note This is an added cost for steps taken 'off-surface'.
        /// @discussion
        ///   Being off-surface includes steps taken at the jumping off point
        ///   and steps taken mid-jump.
        ///   If there is a 1 cell block between the start and the destination,
        ///   assuming going around takes ~3 points, then jumping over with no
        ///   penalty will cost 2 points, jumping over with 1 penalty will cost
        ///   3 points, and so on... If there is a gap between the two surfaces,
        ///   the cost of jumping will aggregate with each step until the agent
        ///   lands on a surface.
        public readonly int JumpPenalty;

        /// Determines how the agent should behave when its destination is on a foreign surface.
        public readonly PathFindingBehaviour Behaviour;

        public AgentConfiguration(int jumpPenalty, float jumpDistance, PathFindingBehaviour behaviour)
        {
            if (jumpPenalty < 0)
                throw new ArgumentException("Jump penalty must be greater or equal to zero.");

            if (jumpDistance < 0)
                throw new ArgumentException("Jump distance must be greater or equal to zero.");

            JumpDistance = jumpDistance;
            JumpPenalty = jumpPenalty;
            Behaviour = behaviour;
        }

        public static AgentConfiguration CreateSimpleAgent()
        {
            return new AgentConfiguration(0, 0f, PathFindingBehaviour.SingleSurface);
        }

        public static AgentConfiguration CreateJumpingAgent
            (PathFindingBehaviour pathFindingBehaviour = PathFindingBehaviour.InterSurfacePreferResults)
        {
            return new AgentConfiguration(2, 1f, pathFindingBehaviour);
        }
    }
}
