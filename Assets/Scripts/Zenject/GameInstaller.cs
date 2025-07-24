namespace Zenject
{
    public class GameInstaller : MonoInstaller
    {
        // Здесь вы можете добавить свои связывания (bindings)
        public override void InstallBindings()
        {
            // Пример: Связывание GameManager как синглтона
             Container.Bind<PlayerController>().FromComponentInHierarchy().AsSingle();
             Container.Bind<UnitManager>().FromComponentInHierarchy().AsSingle();
             Container.Bind<TurnManager>().FromComponentInHierarchy().AsSingle();
            // Пример: Связывание префаба юнита
            // Container.BindFactory<UnitController, UnitController.Factory>().FromComponentInNewPrefab(yourUnitPrefab).AsSingle();
        }
    }
}