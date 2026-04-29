public class BaseServiceBinder : Singleton<BaseServiceBinder>
{
     public  ZFrameworkContainer container;
     protected  BaseServiceBinder()
     {
        container = new ZFrameworkContainer();
        Binder();
      
     }
    public void RegisterObj(object obj)
    {
        container.Inject(obj);
    }
    public virtual void Binder()
    {
        // container.RegisterInstance<ILandMapMgr>(new LandMapMgr());
        // container.RegisterInstance<IWateringHoleMgr>(new WateringHoleMgr());
        // container.RegisterInstance<ICharacterMgr>(new CharacterMgr());
        // container.RegisterInstance<ICCShopMgr>(new CCShopMgr());
        // container.RegisterInstance<IBagMgr>(new BagMgr());
        // container.RegisterInstance<ITestMgr>(new TestMgr());
    }


}
