using UnityEngine;

namespace ProjectPVP.Match
{
    /// <summary>
    /// Coloque este script nas imagens de fundo (SpriteRenderers) que você quer que tenham Parallax.
    /// </summary>
    public class ParallaxController : MonoBehaviour
    {
        [Header("Configurações do Parallax")]
        [Tooltip("1 = Move exatamente junto da camera (Frente). 0 = Fica totalmente parado (Fundo do abismo).")]
        [Range(0f, 1f)]
        public float parallaxEffectMultiplier;

        private Transform cameraTransform;
        private Vector3 lastCameraPosition;

        void Start()
        {
            // Acha a camera principal automaticamente
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
                lastCameraPosition = cameraTransform.position;
            }
            else
            {
                Debug.LogWarning("Nenhuma Câmera com a tag 'MainCamera' foi encontrada para o Parallax acompanhar.");
            }
        }

        void LateUpdate()
        {
            if (cameraTransform == null) return;

            // Calcula quanto a câmera andou desde o último frame
            Vector3 deltaMovement = cameraTransform.position - lastCameraPosition;

            // Move o background apenas numa fração desse movimento
            transform.position += new Vector3(deltaMovement.x * parallaxEffectMultiplier, deltaMovement.y * parallaxEffectMultiplier, 0);

            // Atualiza para o próximo frame
            lastCameraPosition = cameraTransform.position;
        }
    }
}
