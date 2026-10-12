"""用现有 ComfyUI/Qwen Image 2.1 离线生成原创标题图；脚本只关闭自己创建的进程。"""
from pathlib import Path
import argparse
import json
import os
import shutil
import subprocess
import sys
import time
import urllib.request
import traceback

sys.stdout.reconfigure(encoding="utf-8",errors="replace")
sys.stderr.reconfigure(encoding="utf-8",errors="replace")
ROOT=Path(__file__).resolve().parents[1]
COMFY=Path(r"D:\AI\ComfyUI_windows_portable")
DELIVERY=ROOT/"Tools/LocalAI/KeyArt"
PROMPT=("A richly crafted cinematic miniature diorama illustration of a nostalgic Chinese neighborhood in the late 1990s, "
    "warm summer afternoon after school, handmade clay and painted wood materials with soft paper textures. "
    "A charming old terracotta brick alley with moss, pale cream plaster walls, wooden doors, leafy grape trellis and a large old green tree. "
    "On the lower right, three cheerful small children in simple colorful summer shirts and shorts, kneeling around a chalk circle on the ground, "
    "playing with colorful round paper pog discs. The children are viewed mostly from behind and from a three quarter overhead angle. "
    "Orange soda bottles in a crate, striped canvas school bags, a paper airplane, a friendly orange cat sleeping on a low wooden bench. "
    "Golden shafts of sunlight, dappled leaf shadows, amber light, subtle dust motes, pale turquoise and faded ochre accents. "
    "The left third has a softly lit plain cream plaster wall and open space for a game menu, detailed scene clustered on the right. "
    "Elegant premium cozy adventure game key art, expressive stylized shapes, physically believable warm soft lighting, tilt shift depth of field, "
    "a beautiful sense of wonder, intimacy and childhood summer memories. Wide landscape composition. No text, no logos, no watermark.")


def req(base,path,data=None,timeout=30):
    body=None if data is None else json.dumps(data).encode("utf-8")
    request=urllib.request.Request(base+path,body,headers={"Content-Type":"application/json"})
    with urllib.request.urlopen(request,timeout=timeout) as response:return json.load(response)


def graph():
    return {
        "1":{"class_type":"UNETLoader","inputs":{"unet_name":"qwen_image_2.1_int4_convrot.safetensors","weight_dtype":"default"}},
        "2":{"class_type":"CLIPLoader","inputs":{"clip_name":"qwen3vl_8b_w4a8.safetensors","type":"qwen_image","device":"cpu"}},
        "3":{"class_type":"VAELoader","inputs":{"vae_name":"qwen_image_2.1_vae_bf16.safetensors"}},
        "4":{"class_type":"TextEncodeQwenImage21","inputs":{"clip":["2",0],"prompt":PROMPT,"negative_prompt":"","resolution":1024}},
        "5":{"class_type":"EmptyLatentImage","inputs":{"width":1152,"height":640,"batch_size":1}},
        "6":{"class_type":"QwenImage21Cache","inputs":{"model":["1",0],"device":"cpu","dtype":"int8"}},
        "7":{"class_type":"KSampler","inputs":{"model":["6",0],"positive":["4",0],"negative":["4",1],"latent_image":["5",0],
            "seed":2026100301,"steps":25,"cfg":1,"sampler_name":"euler","scheduler":"simple","denoise":1}},
        "8":{"class_type":"VAEDecode","inputs":{"samples":["7",0],"vae":["3",0]}},
        "9":{"class_type":"SaveImage","inputs":{"images":["8",0],"filename_prefix":"ChildhoodGame/summer_keyart"}}
    }


def main():
    parser=argparse.ArgumentParser();parser.add_argument("--endpoint",default="http://127.0.0.1:8191");parser.add_argument("--start-service",action="store_true")
    args=parser.parse_args();DELIVERY.mkdir(parents=True,exist_ok=True)
    st={"success":False,"started_at":time.time(),"source":"local Qwen Image 2.1 int4, ComfyUI", "prompt":PROMPT,
        "models":["qwen_image_2.1_int4_convrot.safetensors","qwen3vl_8b_w4a8.safetensors","qwen_image_2.1_vae_bf16.safetensors"]}
    own_process=None;logs=[]
    def status(): (DELIVERY/"generation_status.json").write_text(json.dumps(st,ensure_ascii=False,indent=2),encoding="utf-8")
    status()
    try:
        for folder,file in [("diffusion_models",st["models"][0]),("text_encoders",st["models"][1]),("vae",st["models"][2])]:
            assert (COMFY/"ComfyUI/models"/folder/file).exists(),"本地模型缺失，停止，不下载："+file
        if args.start_service:
            try:
                req(args.endpoint,"/system_stats",timeout=2)
                print("Using existing ComfyUI endpoint.",flush=True)
            except Exception:
                from urllib.parse import urlparse
                port=urlparse(args.endpoint).port or 8191
                env=os.environ.copy();env.update(HF_HUB_OFFLINE="1",TRANSFORMERS_OFFLINE="1",PYTHONIOENCODING="utf-8")
                logs=[open(DELIVERY/"server.stdout.log","w",encoding="utf-8"),open(DELIVERY/"server.stderr.log","w",encoding="utf-8")]
                own_process=subprocess.Popen([str(COMFY/"python_embeded/python.exe"),"-X","utf8","-s",str(COMFY/"ComfyUI/main.py"),
                    "--windows-standalone-build","--listen","127.0.0.1","--port",str(port),"--disable-auto-launch","--disable-all-custom-nodes",
                    "--cpu-vae","--lowvram","--reserve-vram","1.5"],cwd=COMFY,env=env,stdout=logs[0],stderr=logs[1],creationflags=subprocess.CREATE_NO_WINDOW)
                st["owned_process_id"]=own_process.pid;status()
                for i in range(90):
                    if own_process.poll() is not None:raise RuntimeError("ComfyUI startup failed; see server logs")
                    try: req(args.endpoint,"/system_stats",timeout=1);break
                    except Exception: time.sleep(1)
                else:raise TimeoutError("ComfyUI startup took longer than 90 seconds")
        nodes=req(args.endpoint,"/object_info")
        workflow=graph()
        for n in workflow.values():assert n["class_type"] in nodes,"本地 ComfyUI 缺少节点："+n["class_type"]
        (DELIVERY/"workflow_api.json").write_text(json.dumps(workflow,ensure_ascii=False,indent=2),encoding="utf-8")
        response=req(args.endpoint,"/prompt",{"prompt":workflow,"client_id":"childhood-local-keyart"})
        if "prompt_id" not in response:raise RuntimeError(str(response))
        prompt_id=response["prompt_id"];st.update(phase="generation",prompt_id=prompt_id);status()
        print("Generating one installed-model key art image, prompt "+prompt_id,flush=True)
        for i in range(1800):
            history=req(args.endpoint,"/history/"+prompt_id)
            item=history.get(prompt_id)
            if item:
                outputs=item.get("outputs",{}).get("9",{}).get("images",[])
                if outputs:
                    info=outputs[0];source=COMFY/"ComfyUI/output"/info.get("subfolder","")/info["filename"]
                    target=ROOT/"Assets/Resources/Art/summer_keyart.png";target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
                    st.update(success=True,phase="complete",source_file=str(source),exported_file=str(target),result=item.get("status"));status()
                    print("Generated key art: "+str(target),flush=True);break
                if item.get("status",{}).get("status_str")=="error":raise RuntimeError(json.dumps(item["status"],ensure_ascii=False))
            time.sleep(1)
        else:raise TimeoutError("Image generation exceeded 30 minutes")
    except Exception as exc:
        st["error"]=str(exc);traceback.print_exc()
    finally:
        if own_process and own_process.poll() is None:
            own_process.terminate()
            try:own_process.wait(15)
            except subprocess.TimeoutExpired:own_process.kill()
            st["owned_process_closed"]=True
        for f in logs:f.close()
        st["finished_at"]=time.time();status()
    return 0 if st["success"] else 1


if __name__=="__main__":sys.exit(main())
