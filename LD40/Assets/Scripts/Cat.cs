﻿using System;
using UnityEngine;

public class Cat : MonoBehaviour
{
    [SerializeField] public Transform _raft;
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private SpriteRenderer _spriteRenderer;

    [SerializeField] private Renderer _renderer;
    [SerializeField] private Collider _collider;

    public float SlopeLimit = 3f;
    public float SlideFriction = 0.3f;

    public float MaxSpeed = 0.01f;

    public abstract class CatState { };

    public class Walking : CatState
    {
        public class AttackTarget
        {
            public readonly Cat Me;
            public readonly Cat Target;

            public AttackTarget(Cat me, Cat target)
            {
                Me = me;
                Target = target;
            }

            public void Attack()
            {
                new Fight(Me, Target);
            }
        }

        public class PossibleFight
        {
            public readonly Cat Me;
            public readonly Fight Fight;

            public PossibleFight(Cat me, Fight fight)
            {
                Me = me;
                Fight = fight;
            }

            public void Join()
            {
                Fight.Join(Me);
            }
        }

        public readonly Cat Cat;
        public Transform Waypoint;
        public AttackTarget PossibleAttackTarget;
        public PossibleFight NearbyFight;

        public Walking(Cat cat) { Cat = cat; }

        public void Move(Vector3 direction2D)
        {
            var clampedDirection = Vector2.ClampMagnitude(new Vector2(direction2D.x, direction2D.z), Cat.MaxSpeed);
            var direction3D = new Vector3(clampedDirection.x, -1, clampedDirection.y);

            RaycastHit hit;
            if (Physics.Raycast(Cat.transform.position, Vector3.down, out hit, LayerMask.GetMask("Raft"))) {
                var hitNormal = hit.normal;

                if (Vector3.Angle(Vector3.up, hitNormal) <= Cat.SlopeLimit) {
                    direction3D.x += (1f - hitNormal.y) * hitNormal.x * (1f - Cat.SlideFriction);
                    direction3D.z += (1f - hitNormal.y) * hitNormal.z * (1f - Cat.SlideFriction);
                }
            }

            Cat._characterController.Move(direction3D);

            if (Waypoint != null && Vector3.Distance(Cat.transform.position, Waypoint.position) < 0.2f) {
                Destroy(Waypoint.gameObject);
                Waypoint = null;
            }
        }

        public void SetWaypoint(Vector3 waypointPosition)
        {
            if (Waypoint == null) {
                Waypoint = new GameObject("Waypoint").transform;
                Waypoint.SetParent(Cat._raft);
            }

            Waypoint.position = waypointPosition;
        }

        public void OnDrawGizmos()
        {
            if (Waypoint != null) {
                Gizmos.DrawCube(Waypoint.position, Vector3.one * 0.1f);
                Gizmos.DrawLine(Cat.transform.position, Waypoint.position);
            }

            if (PossibleAttackTarget != null) {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(Cat.transform.position + Vector3.up, 0.3f);
            }

            if (NearbyFight != null) {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(Cat.transform.position + Vector3.up * 1.5f, 0.3f);
            }
        }

        public void OnTriggerEnter(Collider other)
        {
            var edge = other.GetComponent<Edge>();

            if (edge != null) {
                Cat.State = new Hanging();
            } else {
                var anotherCat = other.transform.parent?.GetComponent<Cat>();

                if (anotherCat != null) {
                    PossibleAttackTarget = new AttackTarget(Cat, anotherCat);
                }

                var fight = other.GetComponent<FightView>();

                if (fight != null) {
                    NearbyFight = new PossibleFight(Cat, fight.Fight);
                }
            }
        }

        public void OnTriggerExit(Collider other)
        {
            var anotherCat = other.transform.parent == null ? null : other.transform.parent.GetComponent<Cat>();

            if (anotherCat != null) {
                PossibleAttackTarget = null;
            }

            var fight = other.GetComponent<FightView>();

            if (fight != null) {
                NearbyFight = null;
            }
        }
    }

    public class Hanging : CatState { }
    public class BeingDragged : CatState { }

    public class Fighting : CatState
    {
        public readonly Cat Cat;
        public readonly Fight Fight;

        public Fighting(Cat cat, Fight fight)
        {
            Cat = cat;
            Fight = fight;

            Cat._collider.enabled = false;
        }

        public void Stop()
        {
            Cat.State = new Walking(Cat);
            Cat._collider.enabled = true;
        }
    }

    private CatState _state;

    public CatState State
    {
        get { return _state; }
        set
        {
            _state = value;
            UpdateVisuals();
        }
    }

    private void Start()
    {
        State = new Walking(this);
    }

    public void PickKitty()
    {
        Destroy(gameObject);
    }

    private void Update()
    {
        var walking = _state as Walking;

        if (walking != null && walking.Waypoint != null)
            walking.Move(walking.Waypoint.position - transform.position);
    }

    private void OnDrawGizmos()
    {
        (_state as Walking)?.OnDrawGizmos();
    }

    private void OnTriggerEnter(Collider other)
    {
        (_state as Walking)?.OnTriggerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        (_state as Walking)?.OnTriggerExit(other);
    }

    private void UpdateVisuals()
    {
        _spriteRenderer.sprite = GetSprite();
    }

    private Sprite GetSprite()
    {
        _renderer.enabled = !(_state is Fighting);

        if (_state is Walking)
            return Links.Instance.CatWalkingSprite;
        if (_state is Hanging)
            return Links.Instance.CatHangingSprite;
        if (_state is BeingDragged)
            return Links.Instance.CatDraggedSprite;
        if (_state is Fighting)
            return Links.Instance.CatDraggedSprite;

        throw new ArgumentOutOfRangeException();
    }
}
