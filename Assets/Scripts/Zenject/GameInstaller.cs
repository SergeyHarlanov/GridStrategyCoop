namespace Zenject
{
    public class GameInstaller : MonoInstaller
    {
        // Здесь вы можете добавить свои связывания (bindings)
        public override void InstallBindings()
        {
             Container.Bind<PlayerController>().FromComponentInHierarchy().AsSingle();
             Container.Bind<UnitManager>().FromComponentInHierarchy().AsSingle();
             Container.Bind<TurnManager>().FromComponentInHierarchy().AsSingle();
             Container.Bind<GameManager>().FromComponentInHierarchy().AsSingle();
             Container.Bind<UIManager>().FromComponentInHierarchy().AsSingle();
             Container.Bind<CubeManager>().FromComponentInHierarchy().AsSingle();
        }
    }
}