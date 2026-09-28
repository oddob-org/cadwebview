// 一份适配源码适配三家平台：通过平台宏切换命名空间别名。
// 宏由各 Host 工程定义：ACAD / ZWCAD / GSTARCAD。

#if ACAD
global using AcApp = Autodesk.AutoCAD.ApplicationServices;
global using AcWin = Autodesk.AutoCAD.Windows;
global using AcRt = Autodesk.AutoCAD.Runtime;
global using AcEd = Autodesk.AutoCAD.EditorInput;
global using AcDb = Autodesk.AutoCAD.DatabaseServices;
#elif ZWCAD
// 注意大小写：程序集内实际命名空间为 ZwSoft.ZwCAD.*（非全大写 ZWCAD）
global using AcApp = ZwSoft.ZwCAD.ApplicationServices;
global using AcWin = ZwSoft.ZwCAD.Windows;
global using AcRt = ZwSoft.ZwCAD.Runtime;
global using AcEd = ZwSoft.ZwCAD.EditorInput;
global using AcDb = ZwSoft.ZwCAD.DatabaseServices;
#elif GSTARCAD
global using AcApp = Gssoft.Gscad.ApplicationServices;
global using AcWin = Gssoft.Gscad.Windows;
global using AcRt = Gssoft.Gscad.Runtime;
global using AcEd = Gssoft.Gscad.EditorInput;
global using AcDb = Gssoft.Gscad.DatabaseServices;
#else
#error 未定义平台宏（ACAD / ZWCAD / GSTARCAD 三者之一）
#endif