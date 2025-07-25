namespace Zenject
{
    public class GameInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
             Container.Bind<PlayerController>().FromComponentInHierarchy().AsSingle();
             Container.Bind<UnitManager>().FromComponentInHierarchy().AsSingle();
             Container.Bind<TurnManager>().FromComponentInHierarchy().AsSingle();
             Container.Bind<GameManager>().FromComponentInHierarchy().AsSingle();
             Container.Bind<UIManager>().FromComponentInHierarchy().AsSingle();
             Container.Bind<CubeManager>().FromComponentInHierarchy().AsSingle();
             Container.Bind<GameSettings>().FromComponentInHierarchy().AsSingle();
        }
    }
}