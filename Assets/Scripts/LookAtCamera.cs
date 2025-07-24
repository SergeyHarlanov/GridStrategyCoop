using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    private Camera mainCamera; 

    void Awake()
    {
        mainCamera = Camera.main; 
        if (mainCamera == null)
        {
            Debug.LogError("LookAtCamera: No main camera found in the scene! Make sure your camera has the 'MainCamera' tag.");
        }
    }

    void LateUpdate()
    {
        if (mainCamera == null)
        {
            return;
        }
     
        Vector3 lookDirection = mainCamera.transform.position - transform.position;
        lookDirection.y = 0; // Обнуляем Y, чтобы объект не наклонялся вверх/вниз
        if (lookDirection != Vector3.zero) // Избегаем ошибки при нулевом векторе
        {
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }
}