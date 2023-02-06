using UnityEngine;

namespace Character.State
{
    public abstract class CharacterStateBase 
    {
        public CharacterStateBase(CharacterManager characterManagerRef)
        {
            CharacterManagerRef = characterManagerRef;
        }
        
        public GameplayInputs GameplayInputs;
        public CharacterManager CharacterManagerRef;
        
        public bool CanCharacterMove = true;
        public bool CanCharacterMakeActions = true;
        public float RotationStaticForceY = 0f;
        public float RotationPaddleForceY = 0f;
        
        protected Transform PlayerPosition;

        public abstract void EnterState(CharacterManager character);

        public abstract void UpdateState(CharacterManager character);

        public abstract void FixedUpdate(CharacterManager character);

        public abstract void SwitchState(CharacterManager character);

        public void MakeBoatRotationWithBalance(Transform kayakTransform)
        {
            Vector3 boatRotation = kayakTransform.localRotation.eulerAngles;
            Quaternion targetBoatRotation = Quaternion.Euler(boatRotation.x,boatRotation.y, CharacterManagerRef.Balance * 2);
            kayakTransform.localRotation = Quaternion.Lerp(kayakTransform.localRotation, targetBoatRotation, 0.01f);
            
            Vector3 characterRotation = CharacterManagerRef.transform.rotation.eulerAngles;
            CharacterManagerRef.transform.rotation = Quaternion.Euler(characterRotation.x,characterRotation.y, -boatRotation.z);
        } 
    }
}