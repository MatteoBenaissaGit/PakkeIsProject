using System;
using System.Collections;
using Character.Camera;
using Kayak;
using UnityEngine;
using UnityEngine.Events;

namespace Character.State
{
    public class CharacterNavigationState : CharacterStateBase
    {
        //enum
        public enum Direction
        {
            Left = 0,
            Right = 1
        }

        private enum RotationType
        {
            Static = 0,
            Paddle = 1
        }

        //inputs
        private InputManagement _inputs;
        private float _staticInputTimer;

        //kayak
        private Vector2 _paddleForceValue;
        private float _leftPaddleCooldown, _rightPaddleCooldown;
        private float _currentInputPaddleFrontForce = 30;

        //reference
        private KayakController _kayakController;
        private KayakParameters _kayakValues;
        private Rigidbody _kayakRigidbody;
        private Floaters _floaters;

        #region Constructor

        public CharacterNavigationState(KayakController kayak, InputManagement inputManagement, CharacterManager characterManagerRef, MonoBehaviour monoBehaviour, CameraManager cameraManagerRef) : 
            base(characterManagerRef, monoBehaviour, cameraManagerRef)
        {
            _kayakController = kayak;
            _kayakRigidbody = kayak.Rigidbody;
            _kayakValues = kayak.KayakValues;
            _inputs = inputManagement;
            _inputs.Inputs.DEADZONE = 0.3f;
        }

        #endregion

        #region CharacterBaseState overrided function

        public override void EnterState(CharacterManager character)
        {
            //values
            _rightPaddleCooldown = _kayakValues.PaddleCooldown;
            _leftPaddleCooldown = _kayakValues.PaddleCooldown;
            _staticInputTimer = _kayakValues.StaticRotationCooldownAfterPaddle;
            _floaters = CharacterManagerRef.KayakController.FloatersRef;
                
            //booleans
            CharacterManagerRef.LerpBalanceTo0 = true;
            CanBeMoved = true;
        }

        public override void UpdateState(CharacterManager character)
        {
            PaddleCooldownManagement();
            
            //check balanced -> unbalanced
            if (Mathf.Abs(CharacterManagerRef.Balance) >= CharacterManagerRef.BalanceLimit)
            {
                CameraManagerRef.CanMoveCameraManually = false;
                _kayakController.CanReduceDrag = false;
                
                //switch states
                CharacterUnbalancedState characterUnbalancedState = new CharacterUnbalancedState(_kayakController, _inputs, CharacterManagerRef, MonoBehaviourRef, CameraManagerRef);
                CharacterManagerRef.SwitchState(characterUnbalancedState);

                CameraUnbalancedState cameraUnbalancedState = new CameraUnbalancedState(CameraManagerRef, MonoBehaviourRef);
                CameraManagerRef.SwitchState(cameraUnbalancedState);
            }
            
            MakeBoatRotationWithBalance(_kayakController.transform, 1);
        }

        public override void FixedUpdate(CharacterManager character)
        {
            SetBrakeAnimationToFalse();
            
            if (CanCharacterMove == false)
            {
                StopCharacter();
                return;
            }
            
            if (_inputs.Inputs.PaddleLeft || _inputs.Inputs.PaddleRight)
            {
                HandlePaddleMovement();
            }
            if ((_inputs.Inputs.RotateLeft != 0 || _inputs.Inputs.RotateRight != 0) && _staticInputTimer <= 0)
            {
                HandleStaticRotation();
            }

            KayakRotationManager(RotationType.Paddle);
            KayakRotationManager(RotationType.Static);
            
            VelocityToward();

            CheckRigidbodyFloatersBalance();
        }

        public override void SwitchState(CharacterManager character)
        {
        }

        #endregion

        #region Methods

        /// <summary>
        /// manages the rotation of the kayak based on the input rotation type and updates the relevant rotation force values.
        /// </summary>
        private void KayakRotationManager(RotationType rotationType)
        {
            //get rotation
            float rotationForceY = rotationType == RotationType.Paddle ? RotationPaddleForceY : RotationStaticForceY;

            //calculate rotation
            if (Mathf.Abs(rotationForceY) > 0.001f)
            {
                rotationForceY = Mathf.Lerp(rotationForceY, 0,
                    rotationType == RotationType.Paddle
                        ? _kayakValues.PaddleRotationDeceleration
                        : _kayakValues.StaticRotationDeceleration);
            }
            else
            {
                rotationForceY = 0;
            }

            //apply transform
            Transform kayakTransform = _kayakController.transform;
            kayakTransform.Rotate(Vector3.up, rotationForceY);

            //changes values
            switch (rotationType)
            {
                case RotationType.Paddle:
                    RotationPaddleForceY = rotationForceY;
                    break;
                case RotationType.Static:
                    RotationStaticForceY = rotationForceY;
                    break;
            }
        }

        /// <summary>
        /// Lerp the character velocity to 0
        /// </summary>
        private void StopCharacter()
        {
            Debug.Log("stop character");
            _kayakRigidbody.velocity = Vector3.Lerp(_kayakRigidbody.velocity, Vector3.zero, 0.01f);
        }

        /// <summary>
        /// Set the animators brake booleans to false
        /// </summary>
        private void SetBrakeAnimationToFalse()
        {
            CharacterManagerRef.PaddleAnimator.SetBool("BrakeLeft", false);
            CharacterManagerRef.PaddleAnimator.SetBool("BrakeRight", false);
        }

        #endregion

        #region Paddle Movement

        /// <summary>
        /// Handle the paddling of the kayak, add rotation force, launch the paddleForceCurve method to move the rigidbody, play paddle sound, set animation paddle trigger
        /// </summary>
        private void Paddle(Direction direction)
        {
            //timers
            _kayakController.DragReducingTimer = 0.5f;
            _staticInputTimer = _kayakValues.StaticRotationCooldownAfterPaddle;
            
            //rotation
            float rotation = _kayakValues.PaddleSideRotationForce;
            RotationPaddleForceY += direction == Direction.Right ? -rotation : rotation;

            //balance
            CharacterManagerRef.Balance += RotationPaddleForceY * CharacterManagerRef.RotationToBalanceMultiplier;
            
            //force
            MonoBehaviourRef.StartCoroutine(PaddleForceCurve());

            //audio
            SoundManager.Instance.PlaySound(_kayakController.PaddlingAudioClip);
            
            //animation
            CharacterManagerRef.PaddleAnimator.SetTrigger(direction == Direction.Left ? "PaddleLeft" : "PaddleRight");

            //events
            switch (direction)
            {
                case Direction.Left:
                    OnPaddleLeft.Invoke();
                    break;
                case Direction.Right:
                    OnPaddleRight.Invoke();
                    break;
            }
        }

        /// <summary>
        /// Detect & verify paddle input and launch paddle method 
        /// </summary>
        private void HandlePaddleMovement()
        {
            //input -> paddleMovement
            if (_inputs.Inputs.PaddleLeft && _leftPaddleCooldown <= 0 && _inputs.Inputs.PaddleRight == false)
            {
                _leftPaddleCooldown = _kayakValues.PaddleCooldown;
                _rightPaddleCooldown = _kayakValues.PaddleCooldown / 2;
                Paddle(Direction.Left);
            }
            
            if (_inputs.Inputs.PaddleRight && _rightPaddleCooldown <= 0 && _inputs.Inputs.PaddleLeft == false)
            {
                _rightPaddleCooldown = _kayakValues.PaddleCooldown;
                _leftPaddleCooldown = _kayakValues.PaddleCooldown / 2;
                Paddle(Direction.Right);
            }
        }

        /// <summary>
        /// Add paddle force to the kayak a certain number of times
        /// </summary>
        private IEnumerator PaddleForceCurve()
        {
            for (int i = 0; i <= _kayakValues.NumberOfForceAppliance; i++)
            {
                float x = 1f/_kayakValues.NumberOfForceAppliance * i;
                float force = _kayakValues.ForceCurve.Evaluate(x) * _kayakValues.PaddleForce;
                Vector3 forceToApply = _kayakController.transform.forward * force;
                _kayakRigidbody.AddForce(forceToApply);

                yield return new WaitForSeconds(_kayakValues.TimeBetweenEveryAppliance);
            }
        }

        /// <summary>
        /// Count the different cooldowns
        /// </summary>
        private void PaddleCooldownManagement()
        {
            _leftPaddleCooldown -= Time.deltaTime;
            _rightPaddleCooldown -= Time.deltaTime;

            _staticInputTimer -= Time.deltaTime;
        }

        #endregion

        #region Rotate Movement

        /// <summary>
        ///detect static rotation input and apply static rotation by adding rotation force & setting animator booleans
        /// </summary>
        private void HandleStaticRotation()
        {
            bool isFast = Mathf.Abs(_kayakRigidbody.velocity.x + _kayakRigidbody.velocity.z) >= 0.1f;

            //left
            if (_inputs.Inputs.RotateLeft > _inputs.Inputs.DEADZONE)
            {
                if (isFast)
                {
                    DecelerationAndRotate(Direction.Right);
                }
                RotationStaticForceY += _kayakValues.StaticRotationForce;
                
                CharacterManagerRef.PaddleAnimator.SetBool("BrakeLeft", true);
            }
            
            //right
            if (_inputs.Inputs.RotateRight > _inputs.Inputs.DEADZONE)
            {
                if (isFast)
                {
                    DecelerationAndRotate(Direction.Left);
                }
                RotationStaticForceY -= _kayakValues.StaticRotationForce;
                
                CharacterManagerRef.PaddleAnimator.SetBool("BrakeRight", true);
            }
        }

        /// <summary>
        /// Lerp the kayak velocity to 0 and make add rotation force
        /// </summary>
        private void DecelerationAndRotate(Direction direction)
        {
            Vector3 targetVelocity = new Vector3(0, _kayakRigidbody.velocity.y, 0);
            
            _kayakRigidbody.velocity = Vector3.Lerp(_kayakRigidbody.velocity, targetVelocity,
                _kayakValues.VelocityDecelerationLerp);
            
            float force = _kayakValues.VelocityDecelerationRotationForce;
            
            RotationStaticForceY += direction == Direction.Left ? -force : force;
        }

        #endregion

        #region Wave/Floaters and Balance management
        
        /// <summary>
        /// Check the floater's level to see if the boat is unbalanced
        /// </summary>
        private void CheckRigidbodyFloatersBalance()
        {
            float frontLeftY = _floaters.FrontLeft.transform.position.y;
            float frontRightY = _floaters.FrontRight.transform.position.y;
            float backLeftY = _floaters.BackLeft.transform.position.y;
            float backRightY = _floaters.BackRight.transform.position.y;
            
            float frontLevel = (frontLeftY + frontRightY) / 2;
            float backLevel = (backLeftY + backRightY) / 2;
            float leftLevel = (frontLeftY + backLeftY) / 2;
            float rightLevel = (frontRightY + backRightY) / 2;

            float multiplier = CharacterManagerRef.FloatersLevelDifferenceToBalanceMultiplier;
            float frontBackDifference = Mathf.Abs(frontLevel - backLevel) * multiplier;
            float leftRightDifference = Mathf.Abs(leftLevel - rightLevel) * multiplier;
            
            CharacterManagerRef.AddBalanceValueToCurrentSide(frontBackDifference);
            CharacterManagerRef.Balance += leftRightDifference;
        }

        #endregion
        
    }
}