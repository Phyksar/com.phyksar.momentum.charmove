using UnityEngine;

namespace Momentum.Components
{
    public abstract class ViewMode : MonoBehaviour
    {
        public Camera cameraPrefab;

        private new Camera camera;

        protected abstract void UpdateView(Camera camera);

        protected void DestroyCamera()
        {
            if (camera != null) {
                GameObject.Destroy(camera.gameObject);
                camera = null;
            }
        }

        private void OnEnable()
        {
            if (camera == null && cameraPrefab != null) {
                var cameraInstance = GameObject.Instantiate(cameraPrefab.gameObject);
                cameraInstance.transform.SetParent(transform);
                cameraInstance.TryGetComponent<Camera>(out camera);
            }
        }

        private void OnDisable()
        {
            DestroyCamera();
        }

        private void OnDestroy()
        {
            DestroyCamera();
        }

        private void LateUpdate()
        {
            if (camera != null) {
                UpdateView(camera);
            }
        }
    }
}
