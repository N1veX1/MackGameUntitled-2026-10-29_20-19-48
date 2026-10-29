using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace MaskTime.Editor
{
    public static class TimeTutorialBuilder
    {
        public const string ScenePath="Assets/Scenes/Level05_TimeTutorial.unity";
        public const string Kit="Assets/Логика/5 уровень";
        const string AssetsRoot="Assets/MaskTime";
        static Material concrete,metal,lightPanel,cyan,amber,glass,lettering,dustMat;
        static Font font;static TimeMaskClock clock;
        [MenuItem("Tools/Mask Time/Create Tutorial 5")]
        public static void CreateTutorial(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())Build();}
        [MenuItem("Tools/Mask Time/Open Tutorial 5")]
        public static void Open(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(ScenePath);}
        public static void Build()
        {
            Directory.CreateDirectory(AssetsRoot+"/Materials");Directory.CreateDirectory(Kit);AssetDatabase.Refresh();
            concrete=Mat("Concrete",new Color(.27f,.32f,.35f));metal=Mat("Graphite",new Color(.045f,.065f,.085f));
            lightPanel=Mat("Porcelain",new Color(.64f,.7f,.71f));cyan=Mat("TimeCyan",new Color(.22f,.94f,.8f),true);
            amber=Mat("ScheduleAmber",new Color(1,.61f,.22f),true);glass=Mat("WindowBlue",new Color(.13f,.25f,.33f));
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lettering=AssetDatabase.LoadAssetAtPath<Material>(AssetsRoot+"/Materials/ClockText.mat");
            if(!lettering){lettering=new Material(Shader.Find("MaskTime/DepthText"));lettering.mainTexture=font.material.mainTexture;AssetDatabase.CreateAsset(lettering,AssetsRoot+"/Materials/ClockText.mat");}
            dustMat=Mat("AnomalyDust",new Color(.9f,.35f,.08f),true);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.32f,.39f,.44f);
            RenderSettings.fog=true;RenderSettings.fogColor=new Color(.035f,.055f,.075f);RenderSettings.fogDensity=.005f;RenderSettings.fogMode=FogMode.Exponential;
            var root=Group("05 — Хронологическая станция",null);
            var architecture=Group("01 — Пол и стены",root);var details=Group("02 — Свет и детали",root);
            var trials=Group("03 — Испытания по времени",root);var guidance=Group("04 — Расписания и контрольные точки",root);
            clock=Group("Часы мира — реальное / управляемое время",root).gameObject.AddComponent<TimeMaskClock>();
            Box(architecture,"Пол — вход, дверь, аномалия",new Vector3(0,-.25f,18),new Vector3(8,.5f,44),concrete);
            Box(architecture,"Пол — после моста до выхода",new Vector3(0,-.25f,71),new Vector3(8,.5f,42),concrete);
            Box(architecture,"Левая стена",new Vector3(-4.25f,3,44),new Vector3(.5f,6,96),concrete);
            Box(architecture,"Правая стена",new Vector3(4.25f,3,44),new Vector3(.5f,6,96),concrete);
            Box(architecture,"Потолок",new Vector3(0,6.25f,44),new Vector3(9,.5f,96),concrete);
            Box(architecture,"Стена входа",new Vector3(0,3,-4.25f),new Vector3(8,6,.5f),metal);
            Box(architecture,"Стена выхода",new Vector3(0,3,92.25f),new Vector3(8,6,.5f),metal);
            Box(architecture,"Дно разрыва",new Vector3(0,-12,45),new Vector3(8,.5f,10),metal);
            for(int z=0;z<92;z+=6)
            {
                Box(details,"Ребро потолка",new Vector3(0,5.87f,z),new Vector3(8,.22f,.25f),metal);
                foreach(int side in new[]{-1,1})
                {
                    Box(details,"Световая полоса",new Vector3(side*3.97f,4.2f,z),new Vector3(.04f,.11f,1.6f),z<40?amber:cyan,false);
                    var lamp=Group("Свет станции",details).gameObject.AddComponent<Light>();lamp.transform.position=new Vector3(side*3.4f,4,z);lamp.type=LightType.Point;lamp.range=8;lamp.intensity=1.8f;lamp.color=new Color(.73f,.87f,1);lamp.shadows=LightShadows.None;
                }
            }
            var sun=Group("Мягкий общий свет",details).gameObject.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=.65f;sun.transform.rotation=Quaternion.Euler(48,-25,0);sun.color=new Color(.7f,.81f,.91f);sun.shadows=LightShadows.Soft;
            var door=Door(trials,"01 — Дверь по расписанию",new Vector3(0,0,12),new ClockTime(8,0),new ClockTime(8,15));Save(door,"Дверь — по интервалу времени");
            Board(guidance,"01 / ЧАСЫ", "M — включи управление временем\n1 — часы · 2 — минуты · колесо\nДверь: 08:00:00 — 08:15:00\nВыставь 08:05 и пройди",new Vector3(-3.94f,2.25f,5),Quaternion.Euler(0,-90,0));
            Board(guidance,"МАСКА ВРЕМЕНИ / 05","Без маски часы показывают настоящее\nВ маске время задаёшь ты\n1 — часы · 2 — минуты · 3 — секунды\nM — снять: мир вернётся к настоящему",new Vector3(3.94f,2.25f,3),Quaternion.Euler(0,90,0));
            Checkpoint(guidance,"После первой двери",new Vector3(0,1.3f,17),new Vector3(0,.03f,18));
            var hazard=Hazard(trials,new Vector3(0,0,27));Save(hazard,"Аномалия — безопасный интервал");
            Board(guidance,"02 / БЕЗОПАСНЫЙ ИНТЕРВАЛ","Хрональное поле опасно вне расписания\nБезопасно: 09:30:00 — 09:45:00\nВыставь 09:35 перед входом\nКрасное поле сменится зелёным",new Vector3(-3.94f,2.25f,20),Quaternion.Euler(0,-90,0));
            Checkpoint(guidance,"После аномалии",new Vector3(0,1.3f,34),new Vector3(0,.03f,35));
            var bridge=Bridge(trials,new Vector3(0,0,40));Save(bridge,"Мост — раскладной по времени");
            Board(guidance,"03 / РАСКЛАДНОЙ МОСТ","Мост: 10:15:00 — 10:20:00\nВыставь 10:17 и дождись раскрытия\nСнятие маски вернёт настоящее\nЕсли моста нет — ты упадёшь",new Vector3(-3.94f,2.25f,36),Quaternion.Euler(0,-90,0));
            foreach(float z in new[]{23f,31f,39.8f,50.2f})Box(details,"Граница испытания",new Vector3(0,.015f,z),new Vector3(7.8f,.025f,.11f),amber,false);
            Checkpoint(guidance,"После моста",new Vector3(0,1.3f,53),new Vector3(0,.03f,54));
            var seconds=Door(trials,"04 — Шлюз секунд",new Vector3(0,0,70),new ClockTime(15,20,10),new ClockTime(15,20,20));Save(seconds,"Шлюз — точность до секунды");
            var shutters=Shutters(trials,new Vector3(3.90f,2.5f,64));Save(shutters,"Ставни — открываются по времени");
            Board(guidance,"04 / СЕКУНДЫ","Шлюз и ставни: 15:20:10 — 15:20:20\n1 → 15 часов · 2 → 20 минут\n3 → 15 секунд · проверь часы\nПройди в последнюю комнату",new Vector3(-3.94f,2.25f,60),Quaternion.Euler(0,-90,0));
            Board(guidance,"TUTORIAL 5 / ПРОЙДЕН","Двери, зоны и мосты слушаются часов\nИнтервалы доступны в Inspector\nМаска снимается — время настоящее\nМожно вернуться к любому испытанию",new Vector3(0,2.5f,91.9f),Quaternion.identity);
            Checkpoint(guidance,"Завершение Tutorial 5",new Vector3(0,1.3f,83),new Vector3(0,.03f,80),true);
            var player=Player(root);Save(player,"Игрок — часы времени");Save(clock.transform,"Часы — источник времени");
            string path=AssetDatabase.GenerateUniqueAssetPath(ScenePath);EditorSceneManager.SaveScene(scene,path);AssetDatabase.SaveAssets();
            Debug.Log("TIME_TUTORIAL_BUILT: "+path+"; four trials, physical watch, editable schedules, no mask mesh.");
        }
        static Transform Door(Transform parent,string name,Vector3 position,ClockTime start,ClockTime end)
        {
            var root=Group(name,parent);root.localPosition=position;
            foreach(int side in new[]{-1,1})Box(root,"Боковая часть проёма",new Vector3(side*2.875f,3,0),new Vector3(2.25f,6,.6f),concrete);
            Box(root,"Надпроёмная балка",new Vector3(0,5,0),new Vector3(3.5f,2,.6f),metal);
            var leaf=Box(root,"Подвижная дверь",new Vector3(0,1.95f,0),new Vector3(3.45f,3.9f,.32f),lightPanel).transform;
            // Marks are separate children of the root, so the door's non-uniform scale does not distort them.
            foreach(int side in new[]{-1,1})Box(root,"Свет проёма",new Vector3(side*1.76f,2,-.34f),new Vector3(.055f,4,.04f),cyan,false);
            var safety=Group("Защита от закрытия на игроке",root).gameObject.AddComponent<BoxCollider>();safety.center=new Vector3(0,1.9f,0);safety.size=new Vector3(3.6f,3.8f,1.2f);safety.isTrigger=true;
            var state=root.gameObject.AddComponent<TimeWindowState>();state.Configure(clock,start,end);
            root.gameObject.AddComponent<TimeWindowActuator>().Configure(leaf,new Vector3(0,1.95f,0),new Vector3(0,6.1f,0),Vector3.zero,Vector3.zero,false,safety);
            var text=Text(root,"Расписание двери",new Vector3(0,4.55f,-.36f),.035f,"");text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
            root.gameObject.AddComponent<TimeWindowLabel>().Configure(state,text);return root;
        }
        static Transform Hazard(Transform parent,Vector3 position)
        {
            var root=Group("02 — Хрональная аномалия",parent);root.localPosition=position;
            var area=root.gameObject.AddComponent<BoxCollider>();area.center=Vector3.up*2;area.size=new Vector3(7.9f,4,7);area.isTrigger=true;
            var state=root.gameObject.AddComponent<TimeWindowState>();state.Configure(clock,new ClockTime(9,30),new ClockTime(9,45));
            var particles=Group("Частицы временного поля",root).gameObject.AddComponent<ParticleSystem>();particles.transform.localPosition=Vector3.up*1.9f;
            var main=particles.main;main.startLifetime=2.8f;main.startSpeed=.1f;main.startSize=.055f;main.maxParticles=140;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            main.startColor=new Color(1,.32f,.12f,.5f);var emission=particles.emission;emission.rateOverTime=38;
            var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(7.5f,3.6f,6.8f);
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial=dustMat;
            var glow=Group("Свечение поля",root).gameObject.AddComponent<Light>();glow.transform.localPosition=Vector3.up*2.8f;glow.type=LightType.Point;glow.range=9;glow.intensity=2;glow.color=new Color(1,.3f,.1f);glow.shadows=LightShadows.None;
            root.gameObject.AddComponent<TimeHazard>().Configure(particles,glow);
            for(int side=-1;side<=1;side+=2)Box(root,"Излучатель поля",new Vector3(side*3.8f,1.4f,0),new Vector3(.16f,2.8f,.4f),metal);
            var text=Text(root,"Расписание поля",new Vector3(0,4.4f,-3.7f),.035f,"");text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
            root.gameObject.AddComponent<TimeWindowLabel>().Configure(state,text,"ПОЛЕ СТАБИЛЬНО · ПРОХОДИ","АНОМАЛИЯ · НЕ ВХОДИТЬ");return root;
        }
        static Transform Bridge(Transform parent,Vector3 position)
        {
            var root=Group("03 — Раскладной мост",parent);root.localPosition=position;
            var hinge=Group("Шарнир — подвижная часть",root);
            Box(hinge,"Настил моста",new Vector3(0,-.25f,5),new Vector3(6,.5f,10),lightPanel);
            for(int side=-1;side<=1;side+=2)Box(hinge,"Подсветка края",new Vector3(side*2.88f,.01f,5),new Vector3(.08f,.02f,9.9f),cyan,false);
            for(int z=1;z<10;z++)Box(hinge,"Шов настила",new Vector3(0,.006f,z),new Vector3(5.7f,.012f,.045f),metal,false);
            foreach(int side in new[]{-1,1})Box(root,"Опора шарнира",new Vector3(side*3.2f,-.25f,0),new Vector3(.35f,.8f,.8f),metal);
            var state=root.gameObject.AddComponent<TimeWindowState>();state.Configure(clock,new ClockTime(10,15),new ClockTime(10,20));
            root.gameObject.AddComponent<TimeWindowActuator>().Configure(hinge,Vector3.zero,Vector3.zero,new Vector3(90,0,0),Vector3.zero,true);
            var text=Text(root,"Расписание моста",new Vector3(0,3.3f,-.8f),.035f,"");text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
            root.gameObject.AddComponent<TimeWindowLabel>().Configure(state,text,"МОСТ РАСКРЫВАЕТСЯ","МОСТ СЛОЖЕН");return root;
        }
        static Transform Shutters(Transform parent,Vector3 position)
        {
            var root=Group("04 — Окно со ставнями",parent);root.localPosition=position;root.localRotation=Quaternion.Euler(0,90,0);
            Box(root,"Оконная ниша",new Vector3(0,0,-.02f),new Vector3(3,2.7f,.12f),glass,false);
            Box(root,"Свет за стеклом",new Vector3(0,0,-.10f),new Vector3(2.75f,2.4f,.02f),cyan,false);
            var shutter=Box(root,"Подвижные ставни",new Vector3(0,0,-.17f),new Vector3(2.8f,2.45f,.12f),metal,false).transform;
            for(int i=-1;i<=1;i+=2)Box(root,"Рама",new Vector3(i*1.5f,0,-.18f),new Vector3(.12f,2.8f,.15f),lightPanel,false);
            var state=root.gameObject.AddComponent<TimeWindowState>();state.Configure(clock,new ClockTime(15,20,10),new ClockTime(15,20,20));
            root.gameObject.AddComponent<TimeWindowActuator>().Configure(shutter,new Vector3(0,0,-.17f),new Vector3(0,2.7f,-.17f),Vector3.zero,Vector3.zero);
            return root;
        }
        static Transform Player(Transform parent)
        {
            var root=Group("Игрок — часы на правой стороне",parent);root.localPosition=new Vector3(0,.03f,0);root.gameObject.AddComponent<CharacterController>();
            var camera=Group("Player Camera",root).gameObject.AddComponent<Camera>();camera.transform.localPosition=Vector3.up*1.7f;camera.tag="MainCamera";camera.fieldOfView=74;camera.nearClipPlane=.03f;camera.farClipPlane=140;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=RenderSettings.fogColor;camera.gameObject.AddComponent<AudioListener>();
            root.gameObject.AddComponent<TimeMaskPlayer>().Configure(clock,camera);
            var watch=Group("Часы — без руки и без маски",camera.transform);watch.localPosition=new Vector3(.29f,-.19f,.55f);watch.localRotation=Quaternion.Euler(-8,-10,-3);
            Box(watch,"Ремешок",Vector3.zero,new Vector3(.115f,.37f,.035f),metal,false);
            Box(watch,"Корпус часов",Vector3.zero,new Vector3(.205f,.24f,.05f),lightPanel,false);
            Box(watch,"Ободок",new Vector3(0,0,-.029f),new Vector3(.19f,.225f,.018f),metal,false);
            Box(watch,"Экран",new Vector3(0,0,-.041f),new Vector3(.172f,.19f,.008f),glass,false);
            Box(watch,"Кнопка настройки",new Vector3(.111f,.03f,0),new Vector3(.025f,.045f,.027f),amber,false);
            var mode=Text(watch,"Режим",new Vector3(0,.062f,-.047f),.0019f,"LOCAL / LIVE");mode.anchor=TextAnchor.MiddleCenter;
            var digits=Text(watch,"Часы минуты секунды",new Vector3(0,.005f,-.048f),.0047f,"00:00:00");digits.anchor=TextAnchor.MiddleCenter;digits.richText=true;
            var units=Text(watch,"Выбор разряда",new Vector3(0,-.05f,-.048f),.0018f,"1 H    2 M    3 S");units.anchor=TextAnchor.MiddleCenter;
            watch.gameObject.AddComponent<TimeWatchDisplay>().Configure(clock,digits,mode,units);return root;
        }
        static void Checkpoint(Transform parent,string name,Vector3 p,Vector3 feet,bool finish=false)
        {
            var root=Group(name,parent);root.localPosition=p;var box=root.gameObject.AddComponent<BoxCollider>();box.isTrigger=true;box.size=new Vector3(7.8f,2.6f,.6f);
            var marker=Group("Точка возрождения",root);marker.position=feet;root.gameObject.AddComponent<TimeCheckpoint>().Configure(marker,finish);
        }
        static void Board(Transform parent,string heading,string body,Vector3 p,Quaternion q)
        {
            var root=Group(heading,parent);root.localPosition=p;root.localRotation=q;
            Box(root,"Панель",Vector3.zero,new Vector3(4,2.1f,.05f),metal,false);
            Box(root,"Акцент",new Vector3(-1.85f,0,-.04f),new Vector3(.04f,1.8f,.02f),cyan,false);
            var label=Text(root,"Подсказка",new Vector3(-1.65f,.82f,-.045f),.032f,heading+"\n\n"+body);label.anchor=TextAnchor.UpperLeft;
        }
        static TextMesh Text(Transform parent,string name,Vector3 p,float size,string text)
        {
            var t=Group(name,parent);t.localPosition=p;var mesh=t.gameObject.AddComponent<TextMesh>();mesh.font=font;mesh.fontSize=64;mesh.characterSize=size;mesh.text=text;mesh.color=new Color(.85f,.96f,.95f);t.GetComponent<Renderer>().sharedMaterial=lettering;return mesh;
        }
        static Transform Group(string name,Transform parent){var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
        static GameObject Box(Transform parent,string name,Vector3 p,Vector3 size,Material material,bool solid=true)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=material;
            if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }
        static Material Mat(string name,Color color,bool unlit=false)
        {
            string path=AssetsRoot+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m)return m;
            var shader=Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit");if(!shader)throw new Exception("URP shader missing");
            m=new Material(shader);m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.28f);if(unlit)m.SetFloat("_Cull",0);AssetDatabase.CreateAsset(m,path);return m;
        }
        static void Save(Transform source,string name)
        {
            string path=Kit+"/"+name+".prefab";if(AssetDatabase.LoadAssetAtPath<GameObject>(path))return;
            var copy=Object.Instantiate(source.gameObject);copy.name=name;copy.transform.SetParent(null);copy.transform.position=Vector3.zero;
            PrefabUtility.SaveAsPrefabAsset(copy,path);Object.DestroyImmediate(copy);
        }
    }
}
