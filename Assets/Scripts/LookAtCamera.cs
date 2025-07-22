using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    private Camera mainCamera; // Ссылка на основную камеру сцены

    void Awake()
    {
        // В Awake пытаемся найти основную камеру.
        // Это делается здесь, чтобы убедиться, что камера найдена до того, как ее нужно будет использовать в LateUpdate.
        // Если камер несколько, убедитесь, что тег "MainCamera" установлен корректно.
        mainCamera = Camera.main; 
        if (mainCamera == null)
        {
            Debug.LogError("LookAtCamera: No main camera found in the scene! Make sure your camera has the 'MainCamera' tag.");
        }
    }

    void LateUpdate()
    {
        // Проверяем, что камера найдена, прежде чем пытаться использовать ее.
        if (mainCamera == null)
        {
            return;
        }

        // Поворачиваем объект так, чтобы его передняя ось (Z-ось) смотрела на камеру.
        // Vector3.forward * -1 - это то же самое, что Vector3.back, но явно указывает,
        // что мы хотим, чтобы объект смотрел от камеры, а не к ней, 
        // так как transform.LookAt обычно заставляет объект смотреть ВПЕРЕД (по его локальной Z) на цель.
        // Если вы хотите, чтобы объект был всегда "лицом" к камере, используйте 'transform.forward = mainCamera.transform.forward'.
        // Если вы хотите, чтобы он смотрел НА камеру (например, для 2D спрайтов, чтобы они не были перевернуты),
        // используйте 'transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward)'.
        // Здесь мы используем LookAt для простоты, заставляя объект "смотреть" на позицию камеры.
        // Чтобы избежать наклона по оси X (если камера выше/ниже объекта), можно обнулить Y-компоненту.

        // Вариант 1: Объект всегда смотрит на камеру (может наклоняться по X, Y)
        // transform.LookAt(mainCamera.transform.position);

        // Вариант 2: Объект всегда смотрит на камеру, но остается вертикальным (не наклоняется по X)
        Vector3 lookDirection = mainCamera.transform.position - transform.position;
        lookDirection.y = 0; // Обнуляем Y, чтобы объект не наклонялся вверх/вниз
        if (lookDirection != Vector3.zero) // Избегаем ошибки при нулевом векторе
        {
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }
        
        // Дополнительно: Если вы хотите, чтобы объект смотрел "от" камеры (то есть, чтобы его "зад" был виден, а "лицо" смотрело на камеру)
        // Пример: health bar над головой, который должен быть всегда повернут к игроку.
        // transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);
    }
}