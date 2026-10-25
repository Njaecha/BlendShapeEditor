using UnityEngine;

namespace BlendShapeEditor
{
	/// The mirror plane. Stored as a pose relative to Anchor (the session's Space) so it follows the
	/// character/item when it moves; the plane's normal is the pose's local X axis. All reflection happens
	/// in world space, which stays correct for rotated and non-uniformly scaled transforms.
	public class MirrorPlane
	{
		/// Snapshot of the plane's pose, for undo.
		public struct State
		{
			public Transform Anchor;
			public Vector3 LocalOrigin;
			public Quaternion LocalRotation;

			public bool SameAs(State other)
			{
				return Anchor == other.Anchor && LocalOrigin == other.LocalOrigin && LocalRotation == other.LocalRotation;
			}
		}

		public State GetState()
		{
			return new State { Anchor = Anchor, LocalOrigin = LocalOrigin, LocalRotation = LocalRotation };
		}

		public bool SetState(State state)
		{
			return SetLocal(state.Anchor, state.LocalOrigin, state.LocalRotation);
		}

		public Transform Anchor { get; private set; }
		public Vector3 LocalOrigin { get; private set; }
		public Quaternion LocalRotation { get; private set; } = Quaternion.identity;

		public Vector3 OriginWorld => Anchor ? Anchor.TransformPoint(LocalOrigin) : LocalOrigin;
		public Quaternion RotationWorld => Anchor ? Anchor.rotation * LocalRotation : LocalRotation;
		public Vector3 NormalWorld => RotationWorld * Vector3.right;

		/// Plane perpendicular to `anchor`'s local axis (0 = X, 1 = Y, 2 = Z), crossing it at `center`.
		/// Returns false when the plane was already exactly that.
		public bool SetAxisPlane(Transform anchor, int axis, float center)
		{
			Vector3 dir = AxisDir(axis);
			return SetLocal(anchor, dir * center, Quaternion.FromToRotation(Vector3.right, dir));
		}

		/// Plane through `target`'s position, perpendicular to its local axis (0 = X, 1 = Y, 2 = Z), stored
		/// relative to `anchor`. A one-time placement: it doesn't follow `target` afterwards.
		public bool SnapToTransform(Transform anchor, Transform target, int axis)
		{
			Quaternion rotation = target.rotation * Quaternion.FromToRotation(Vector3.right, AxisDir(axis));
			return SetFromWorld(anchor, target.position, rotation);
		}

		/// Plane halfway between two points, its normal pointing from `a` to `b`. The in-plane rotation (which
		/// doesn't affect mirroring) keeps the plane's up axis as close to `upHint` as possible.
		public bool SetFromPoints(Transform anchor, Vector3 aWorld, Vector3 bWorld, Vector3 upHint)
		{
			Vector3 normal = (bWorld - aWorld).normalized;
			Vector3 forward = Vector3.Cross(normal, upHint);
			if (forward.sqrMagnitude < 1e-6f)
				forward = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.forward);
			forward.Normalize();
			Vector3 up = Vector3.Cross(forward, normal);
			// LookRotation's right axis is Cross(up, forward), which is `normal` here
			return SetFromWorld(anchor, (aWorld + bWorld) * 0.5f, Quaternion.LookRotation(forward, up));
		}

		public bool SetFromWorld(Transform anchor, Vector3 originWorld, Quaternion rotationWorld)
		{
			if (!anchor)
				return SetLocal(null, originWorld, rotationWorld);
			return SetLocal(anchor, anchor.InverseTransformPoint(originWorld), Quaternion.Inverse(anchor.rotation) * rotationWorld);
		}

		public Vector3 ReflectPoint(Vector3 world)
		{
			Vector3 n = NormalWorld;
			return world - 2f * Vector3.Dot(world - OriginWorld, n) * n;
		}

		public Vector3 ReflectVector(Vector3 world)
		{
			Vector3 n = NormalWorld;
			return world - 2f * Vector3.Dot(world, n) * n;
		}

		/// Reflects a point given in `space`'s local coordinates, returning it in the same space.
		public Vector3 ReflectSpacePoint(Transform space, Vector3 local)
		{
			if (!space)
				return ReflectPoint(local);
			return space.InverseTransformPoint(ReflectPoint(space.TransformPoint(local)));
		}

		/// A rotation of `angle` degrees about world `axis`, mirrored: the axis is reflected and the
		/// direction flips, because a reflection reverses handedness.
		public void ReflectRotation(Vector3 axis, float angle, out Vector3 mirroredAxis, out float mirroredAngle)
		{
			mirroredAxis = ReflectVector(axis);
			mirroredAngle = -angle;
		}

		private bool SetLocal(Transform anchor, Vector3 origin, Quaternion rotation)
		{
			if (Anchor == anchor && LocalOrigin == origin && LocalRotation == rotation)
				return false;
			Anchor = anchor;
			LocalOrigin = origin;
			LocalRotation = rotation;
			return true;
		}

		private static Vector3 AxisDir(int axis)
		{
			switch (axis)
			{
				case 0: return Vector3.right;
				case 1: return Vector3.up;
				default: return Vector3.forward;
			}
		}
	}
}
