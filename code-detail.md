
# Code

## Mid_server_8080

`Mid_server_8080` is the implement of `mid-server` in paper, which is a Node.js program.
What kind of hardware/software platfrom does NOT matter, but it must support to deploy Node.js V12+ LTS which is the vital runtime for mid-server.

By the command of `npm start`, the service would be deploy and execute. In general, npm will be installed automatically by Node.js installing program. However, you must ensure npm had  been installed successfully before launching service.

### code

`Server.js` is the main file in this service, in which `nodejs-websocket` is imported. At initiation, an empty string would be generated for `packages`. 

The service will start and bind with a special network port, by `listen(8080)`. Every websocket request comes, the service will execute `connection.on`. Message content will be set as `str`. When clients connect in first time, the content are always their client name: "From" or "To", which is implement in client-side.

If the client name is "from", the content would be set as the value of `packages` since the second time. If name is "To", the program will hold a loop in connection by `setInterval` which is a Javascript-native method and would execute every 35ms, permanently. In each fragment, the connection will send `package` to client. This system makes message could be transfer from "From" client to "To" client.



## IOS clients

`Unity_ARFoundation_IOS_clients` is the implement of IOS program under the framework of Unity 3D. In the project, the version of unity is "2020.1.0b14" that is the only version the execute the file. Any other version of Unity will lead to unexpected error!!!

### code

This code leverage the ARFoundation example Repo from Unity 3D.[1] Especially the scene of 'HumanBodyTracking3D'. The hold part of network transmission and real time synchronization were implement by myself.

Under the path of "./Assets/MyScripts", `MainLogic.cs` is the websocket code which import Best HTTP/2. There are a few of re-call method for Websocket Operations, such as `OnMessageReceived`, `OnClosed` and `OnOpen`. The method of Start() is the default entrancy of Unity platform and be loaded automatically, when Unity launchs.

Once message arrives from mid-server, OnMessageReceived will be executed. It would execute `ApplyBonePoseFromServer(message)` to unpackage message content to joints information.

Unity provide basic scenario for easy development. This project leverage the scene of 'HumanBodyTracking3D' under the path of "./Assets/HumanTracking". The `MainLogic.cs` is loaded as a sub-object so that WebSocket operations is integrated in this scene. The key sence is `Human Body Tracking` in which the camera would detect and analytics human body and calculate joints coordinates. The project hack in this code and add sendText() at the end of detect method, after calculation. so that all informations will be sent mid-server.


The other vital scenario is Anchors, under the path of "./Assets/Scenes". It contains two AR sub-objects: "AR session" and "AR Session Origin", just like the scene of 'HumanBodyTracking3D'. As well as 'HumanBodyTracking3D', `MainLogic.cs` is also imported for WebSocket ability. Because message content is used to operate robot joints, the robot is imported under the script of `MainLogic.cs`. According to this series operations, the robot model could be loaded in AR room. Once message comes, the Websocket will execute `OnMessageReceived()` and `ApplyBonePoseFromServer(message)` to change robot joints.


## Appendix

[1]: AR Foundation, Unity Technology, https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@1.0/manual/index.html