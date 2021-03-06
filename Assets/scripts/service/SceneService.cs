﻿using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;
using System;

namespace SGK {
    public class SceneService : MonoBehaviour, IService {
        public static float fadeDuration = 0.0f;

        public event System.Action BeforeSceneUnload;
        public event System.Action AfterSceneLoad;
        public event System.Action AfterSceneReady;

        public LoadSceneMode loadSceneMode;

        public CanvasGroup faderCanvasGroup;
        public GameObject loadingView;
        public LoadingAnimate loadingAnimate;

        public Slider processBar;
        public Text processMessage;
        public Text versionText;
        public GameObject loadingImage;
        public GameObject patchImage = null;

        LoadingAnimate loadingScene;

        public UnityEngine.UI.RawImage bgImage;
        public Texture [] loadingImages = new Texture[0];

        bool isFading = false;
        float targetAlpha = 0f;

        private bool isLoading = false;

        public const int PRESISTENT_SCENE_BUILD_INDEX = 1;

        static SceneService _instance;
        public static SceneService GetInstance() {
            return _instance;
        }

        int _sceneIndex = 0;
        public int sceneIndex {
            get {
                return _sceneIndex;
            }
        }

        public void Register(XLua.LuaEnv luaState) {
            _instance = this;
            luaState.Global.Set("SceneService", this);
        }

        public void Dispose() {
        }

        private void Start() {
            // loadingView.SetActive(isLoading);

            // Once the scene is finished loading, start fading in.
            bool sceneLoaded = false;
            for (int i = 0; i < SceneManager.sceneCount; i++) {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.buildIndex != PRESISTENT_SCENE_BUILD_INDEX) {
                    sceneLoaded = true;
                }
            }
        }

        private static bool loading = false;

        public static bool persistentSceneLoaded {
            get {
                return LuaController.GetLuaState() != null;
            }
        }

        public static IEnumerator LoadPersistentScene() {
            if (!loading) {
                loading = true;
                if (LuaController.GetLuaState() == null) {
			        yield return SceneManager.LoadSceneAsync (PRESISTENT_SCENE_BUILD_INDEX, LoadSceneMode.Single);
                }
            }

            while (LuaController.GetLuaState () == null) {
                yield return null;
            }
        }

        public void Reload() {
            // GameObject obj = new GameObject();
            // obj.AddComponent<GameReload>();
            SceneManager.LoadScene("loading");
        }

        public void StartLoading() {
            isLoading = true;
            if (loadingScene != null) {
                loadingScene.StartLoading();
            } else {
                //processBar.gameObject.SetActive(true);
                //processMessage.gameObject.SetActive(true);
                //processBar.value = 0;
            }
        }

        public void SetPercent(float percent, string msg = "") {
            if (loadingScene != null) {
                loadingScene.SetPercent(percent);
            }

            if (processBar != null) {
                processBar.value = percent;
            }

            if (processMessage != null) {
                processMessage.text = msg;
            }
        }

        public void FinishLoading() {
            isLoading = false;
            if (loadingScene != null) {
                loadingScene.FinishLoading();
            }
        }

        bool _unloadOnNextScene = false;
        public void UnloadOnNextScene() {
            _unloadOnNextScene = true;
        }


#region switch scene blank
        static int process = 0;
        string blankScene = "blank";
        IEnumerator LoadingbarCoroutine;

        protected class WaitProcess : CustomYieldInstruction
        {
            int c = 0;
            public override bool keepWaiting
            {
                get
                {
                    return Time.frameCount == c;
                }
            }

            public WaitProcess(int max)
            {
                c = Time.frameCount;
                process += 5;
                process = process > max ? max : process;
            }
        }


        IEnumerator SwitchSceneUseBlankScene(string sceneName, bool useAnimate, bool useFade, string tips, System.Action callback)
        {
            Scene c = SceneManager.GetActiveScene();
            Scene b = SceneManager.GetSceneByName(blankScene);
            
            if (!b.isLoaded)
            {
                SceneManager.LoadScene(blankScene, LoadSceneMode.Additive);
                yield return new WaitProcess(10);
                b = SceneManager.GetSceneByName(blankScene);
            }
            yield return new WaitProcess(20);
            {
                var op = SceneManager.UnloadSceneAsync(c);
                while (!op.isDone)
                {
                    yield return new WaitProcess(40);
                }
            }
            
            {
                var op = AssetManager.UnloadAll();
                while (op != null && !op.isDone)
                {
                    yield return new WaitProcess(50);
                }
                System.GC.Collect();
                LuaController.Collect();
            }
            
            {
                // AssetManager.LoadDepenedBundle();
                // ResourcesManager.LoadUnloadAsset();
                AssetManager.LoadScenes(sceneName); 
                if (BeforeSceneUnload != null)
                {
                    BeforeSceneUnload();
                }
                var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                while(!op.isDone)
                {
                    yield return new WaitProcess(80);
                }
                if (AfterSceneReady != null)
                {
                    AfterSceneReady();
                }
                yield return null;
            }
            
            {
                SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
                yield return new WaitProcess(90);
                if (callback != null)
                {
                    callback();
                    callback = null;
                }
            }
            process = 100;
        }

        IEnumerator LoadingbarThread(string tips) 
        {
            loadingView.SetActive(true);
            processBar.gameObject.SetActive(true);
            processMessage.gameObject.SetActive(true);
            versionText.gameObject.SetActive(false);

            processMessage.text = tips;

            faderCanvasGroup.blocksRaycasts = true;
            faderCanvasGroup.alpha = 1f;

            
            SetPercent(0);
             
            do 
            {
                SetPercent(process / 100.0f);
                yield return null;
            } while (process < 100);
            SetPercent(1f);
            yield return new WaitForEndOfFrame();
            loadingView.SetActive(false);

            faderCanvasGroup.blocksRaycasts = false;
            faderCanvasGroup.alpha = 0f;
        }
#endregion
        public void SwitchScene(string sceneName, bool useAnimate = false, bool useFade = true, string tips = "", System.Action callback = null)
        {
            process = 0;
            if (LoadingbarCoroutine != null)
            {
                StopCoroutine(LoadingbarCoroutine);
            }
            LoadingbarCoroutine = LoadingbarThread(tips);
            StartCoroutine(LoadingbarCoroutine);
            StartCoroutine(SwitchSceneUseBlankScene(sceneName, useAnimate, useFade, tips, callback));
            return;
            
            if (useAnimate && loadingAnimate != null) {
                loadingView.SetActive(false);
                processBar.gameObject.SetActive(false);
                processMessage.gameObject.SetActive(false);
                versionText.gameObject.SetActive(false);

                loadingScene = Instantiate<LoadingAnimate>(loadingAnimate);
                loadingScene.Load(sceneName, true, callback, _unloadOnNextScene);
                if (true) {
                    loadingScene.BeforeSceneUnload = ()=> {
                        if (BeforeSceneUnload != null) {
                            BeforeSceneUnload();
                        }
                    };

                    loadingScene.AfterSceneLoad    = ()=> {
                        _unloadOnNextScene = false;
                        _sceneIndex ++;
                        if (AfterSceneLoad != null) {
                            AfterSceneLoad();
                        }
                    };

                    loadingScene.AfterSceneReady   = ()=> {
                        if (AfterSceneReady!=null) {
                            AfterSceneReady();
                        }
                        loadingScene = null; 
                        faderCanvasGroup.blocksRaycasts = false;
                        faderCanvasGroup.alpha = 0;
                        // faderCanvasGroup.gameObject.SetActive(false);
                    };

                    faderCanvasGroup.gameObject.SetActive(true);
                    faderCanvasGroup.blocksRaycasts = true;
                    loadingScene.StartLoading();
                    loadingScene.tipsText.text = tips;
                }
            } else {
                loadingView.SetActive(true);
                processBar.gameObject.SetActive(false);
                processMessage.gameObject.SetActive(false);
                versionText.gameObject.SetActive(false);

                StartCoroutine(SwitchSceneAndSetActive(sceneName, useFade, tips, callback));
            }
        }

        System.Action scene_loaded_callback = null;
        bool coroutine_waiting_scene_loaded = false;
        private void OnEnable() {
            SceneManager.sceneLoaded += onSceneLoaded;
            SceneManager.sceneUnloaded += onsceneUnloaded;
        }

        private void OnDisable() {
            SceneManager.sceneLoaded -= onSceneLoaded;
            SceneManager.sceneUnloaded -= onsceneUnloaded;
        }

        private void onsceneUnloaded(Scene scene) {
        }

        private void onSceneLoaded(Scene scene, LoadSceneMode mode) {
            if (coroutine_waiting_scene_loaded) {
                coroutine_waiting_scene_loaded = false;
                return;
            }

            if (AfterSceneLoad != null)
                AfterSceneLoad();

            if (scene_loaded_callback != null) {
                scene_loaded_callback();
            }
            scene_loaded_callback = null;

            if (AfterSceneReady != null) {
                AfterSceneReady();
            }

            //faderCanvasGroup.blocksRaycasts = false;
            //faderCanvasGroup.alpha = 0f;
            //loadingImage.SetActive(false);
            //loadingView.SetActive(false);
        }

        public void Callback(int i) {
            if (i == 0) {
                if (BeforeSceneUnload != null) {
                    Debug.LogFormat("SceneService:BeforeSceneUnload");
                    BeforeSceneUnload();
                }
            } else if (i == 1) {
                if (AfterSceneLoad != null) {
                    Debug.LogFormat("SceneService:AfterSceneLoad {0}", "-");
                    AfterSceneLoad();
                }
            } else if (i == 2) {
                Debug.LogFormat("SceneService:AfterSceneReady {0}", "-");
                if (AfterSceneReady != null) {
                    AfterSceneReady();
                }
            }
        }

        public IEnumerator SwitchSceneAndSetActive(string sceneName, bool fade, string tips = "",System.Action callback = null) {
            isLoading = false;
            coroutine_waiting_scene_loaded = true;

            faderCanvasGroup.gameObject.SetActive(true);
            faderCanvasGroup.blocksRaycasts = true;
            // faderCanvasGroup.alpha = 0f;
            processMessage.text = tips;

            if (fade) {
                yield return Fade(1f);
            }

            if (loadSceneMode == LoadSceneMode.Single) {
                if (BeforeSceneUnload != null)
                    BeforeSceneUnload();
            } else {
                Scene activeScene = SceneManager.GetActiveScene();

                // Unload the current active scene.
                if (activeScene.buildIndex != PRESISTENT_SCENE_BUILD_INDEX) {
                    if (BeforeSceneUnload != null)
                        BeforeSceneUnload();

                    if (loadSceneMode == LoadSceneMode.Additive) {
                        yield return SceneManager.UnloadSceneAsync(activeScene);
                    }
                }

                yield return null;
            }

            _sceneIndex ++;

            if (_unloadOnNextScene) {
                _unloadOnNextScene = false;
                SGK.ResourcesManager.UnloadUnusedAssets();
            }

            // load next scene
            if (sceneName == null || sceneName == "") {
                yield return SceneManager.LoadSceneAsync(1, loadSceneMode);
            } else {
                ResourceBundle.LoadScenes(sceneName);
                yield return SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
            }

            Scene newlyLoadedScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            SceneManager.SetActiveScene(newlyLoadedScene);

            while (coroutine_waiting_scene_loaded) {
                yield return null;
            }

            if (AfterSceneLoad != null)
                AfterSceneLoad();

            if (callback != null) {
                callback ();
            }

            while(isLoading) {
                yield return null;
            }

            if (AfterSceneReady != null) {
                AfterSceneReady();
            }

            if (fade) {
                yield return Fade(0f);
            }

            faderCanvasGroup.blocksRaycasts = false;
            faderCanvasGroup.alpha = 0f;
			loadingImage.SetActive(false);
            // faderCanvasGroup.gameObject.SetActive(false);

            loadingView.SetActive(false);
        }

        private IEnumerator Fade(float finalAlpha) {
            if (fadeDuration <= 0) {
                targetAlpha = finalAlpha;
                faderCanvasGroup.alpha = finalAlpha;
                yield break;
            }

            targetAlpha = finalAlpha;
            if (isFading) {
                yield break;
            }

            isFading = true;

            float fadeSpeed = 1f / fadeDuration;
            while (!Mathf.Approximately(faderCanvasGroup.alpha, targetAlpha)) {
                faderCanvasGroup.alpha = Mathf.MoveTowards(faderCanvasGroup.alpha, targetAlpha,
                    fadeSpeed * Time.deltaTime);
                ChangeLoadingImage();
                yield return null;
            }

            isFading = false;
        }

        void ChangeLoadingImage() {
            loadingImage.SetActive(faderCanvasGroup.alpha > 0.1f);
            if (faderCanvasGroup.alpha <= 0.1f && patchImage) {
                Destroy(patchImage);
                patchImage = null;
            }
        }
    }
}
