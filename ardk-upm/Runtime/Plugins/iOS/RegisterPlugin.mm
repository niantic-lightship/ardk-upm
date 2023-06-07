// Put this into LightshipArPlugin/Runtime/Plugins/iOS

#import "UnityAppController.h"
#import "IUnityInterface.h"

@interface MyAppController : UnityAppController
{
}
-(void)preStartUnity;
@end

@implementation MyAppController

-(void)preStartUnity
{
    UnityRegisterRenderingPluginV5(&UnityPluginLoad, &UnityPluginUnload);
    [super preStartUnity];
}
@end

IMPL_APP_CONTROLLER_SUBCLASS(MyAppController)
