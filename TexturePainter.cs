/// <summary>
/// CodeArtist.mx 2015
/// This is the main class of the project, its in charge of raycasting to a model and place brush prefabs infront of the canvas camera.
/// If you are interested in saving the painted texture you can use the method at the end and should save it to a file.
/// </summary>

using Leap;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;

public enum Painter_BrushMode{PAINT,DECAL};
public class TexturePainter : MonoBehaviour {
	public GameObject brushCursor,brushContainer; //The cursor that overlaps the model and our container for the brushes painted
	public Camera sceneCamera,canvasCam;  //The camera that looks at the model, and the camera that looks at the canvas.
	public Sprite cursorPaint,cursorDecal; // Cursor for the differen functions 
	public RenderTexture canvasTexture; // Render Texture that looks at our Base Texture and the painted brushes
	public Material baseMaterial; // The material of our base texture (Were we will save the painted texture)
	public GameObject mainCameraObject;

	Painter_BrushMode mode; //Our painter mode (Paint brushes or decals) 
	float brushSize=1.0f; //The size of our brush 
	Color brushColor; //The selected color 
	int brushCounter=0,MAX_BRUSH_COUNT=1000; //To avoid having millions of brushes 
	bool saving=false; //Flag to check if we are saving the texture 
	
	/** LeapMotionのコントローラー */ 
	private Controller controller; 
	//Dropdownを格納する変数 
	[SerializeField] private Dropdown dropdown; 
	int dominatedHand = 1; 
	int mouseHand = 0;
	
    // 手をアタッチするとこ
    [SerializeField] private GameObject rightHand;
    [SerializeField] private GameObject leftHand;

    [SerializeField] private LineRenderer lRend;

	/** エントリポイント */ 
	void Start() { 
		// LeapMotionのコントローラー
        controller = new Controller();
		// LeapMotionから手の情報を取得 
		var handsStream = this.UpdateAsObservable() 
			.Select(_ => controller.Frame().Hands);
       
        	// グー終了判定ストリーム
        var endRockGripStream = handsStream
            .Where(hands => !IsRockGrip(hands));
       
    }
	
	void Update () {
        bool check = false;
        List<Hand> hands = controller.Frame().Hands;
		brushColor = ColorSelector.GetColor ();	//Updates our painted color with the selected color
		if(hands.Count == 2){
			CheckHand();
            check = true;
		}else if(hands.Count == 1){
			if(dropdown.value == 0 && hands[0].IsRight || dropdown.value == 1 && hands[0].IsLeft){
				dominatedHand = 0;
                check = true;
			}
        }
        if(check){
			if (IsRockGrip(controller.Frame().Hands)) {
				DoAction();
			}
			UpdateBrushCursor ();
        }

	}

	void CheckHand(){
		int left = 0;
		int right = 1;
		List<Hand> hands = controller.Frame().Hands;
		if(hands.Count == 1){
			dominatedHand = 0;
		}else if(hands.Count == 2){
			if(hands[0].IsRight){
				left = 1;
				right = 0;
			}
			if(dropdown.value == 0){
				dominatedHand = right;
				mouseHand = left;
			}else if(dropdown.value == 1){
				dominatedHand = left;
				mouseHand = right;
			}
		}
	}

	//The main action, instantiates a brush or decal entity at the clicked position on the UV map
	void DoAction(){	
		if (saving)
			return;
		Vector3 uvWorldPosition=Vector3.zero;		
		if(HitTestUVPosition(ref uvWorldPosition)){
			GameObject brushObj;
			if(mode==Painter_BrushMode.PAINT){
				brushObj=(GameObject)Instantiate(Resources.Load("TexturePainter-Instances/BrushEntity")); //Paint a brush
				brushObj.GetComponent<SpriteRenderer>().color=brushColor; //Set the brush color
			}
			else{
				brushObj=(GameObject)Instantiate(Resources.Load("TexturePainter-Instances/DecalEntity")); //Paint a decal
			}
			brushColor.a=brushSize*2.0f; // Brushes have alpha to have a merging effect when painted over.
			brushObj.transform.parent=brushContainer.transform; //Add the brush to our container to be wiped later
			brushObj.transform.localPosition=uvWorldPosition; //The position of the brush (in the UVMap)
			brushObj.transform.localScale=Vector3.one*brushSize;//The size of the brush
		}
		brushCounter++; //Add to the max brushes
		if (brushCounter >= MAX_BRUSH_COUNT) { //If we reach the max brushes available, flatten the texture and clear the brushes
			brushCursor.SetActive (false);
			saving=true;
			Invoke("SaveTexture",0.1f);
			
		}
	}

	//To update at realtime the painting cursor on the mesh
	void UpdateBrushCursor(){
		Vector3 uvWorldPosition=Vector3.zero;
		if (HitTestUVPosition (ref uvWorldPosition) && !saving) {
			brushCursor.SetActive(true);
			brushCursor.transform.position =uvWorldPosition+brushContainer.transform.position;									
		} else {
			brushCursor.SetActive(false);
		}		
	}

	//ここをleapmotionの座標に変える
	//Returns the position on the texuremap according to a hit in the mesh collider
	bool HitTestUVPosition(ref Vector3 uvWorldPosition){
		RaycastHit hit;
		Ray cursorRay = handRay();
		if (Physics.Raycast(cursorRay,out hit,200)){
			MeshCollider meshCollider = hit.collider as MeshCollider;
			if (meshCollider == null || meshCollider.sharedMesh == null)
				return false;			
			Vector2 pixelUV  = new Vector2(hit.textureCoord.x,hit.textureCoord.y);
			uvWorldPosition.x=pixelUV.x-canvasCam.orthographicSize;//To center the UV on X
			uvWorldPosition.y=pixelUV.y-canvasCam.orthographicSize;//To center the UV on Y
			uvWorldPosition.z=0.0f;
			return true;
		}
		else{		
			return false;
		}
	}
		
	//Sets the base material with a our canvas texture, then removes all our brushes
	void SaveTexture(){		
		Debug.Log("saveTexture");
		brushCounter=0;
		System.DateTime date = System.DateTime.Now;
		RenderTexture.active = canvasTexture;
		Texture2D tex = new Texture2D(canvasTexture.width, canvasTexture.height, TextureFormat.RGB24, false);		
		tex.ReadPixels (new Rect (0, 0, canvasTexture.width, canvasTexture.height), 0, 0);
		tex.Apply ();
		RenderTexture.active = null;
		baseMaterial.mainTexture =tex;	//Put the painted texture as the base
		foreach (Transform child in brushContainer.transform) {//Clear brushes
			Destroy(child.gameObject);
		}
		StartCoroutine ("SaveTextureToFile"); //Do you want to save the texture? This is your method!
		Invoke ("ShowCursor", 0.1f);
	}

	//Show again the user cursor (To avoid saving it to the texture)
	void ShowCursor(){	
		saving = false;
	}

	////////////////// PUBLIC METHODS //////////////////

	public void SetBrushMode(Painter_BrushMode brushMode){ //Sets if we are painting or placing decals
		mode = brushMode;
		brushCursor.GetComponent<SpriteRenderer> ().sprite = brushMode == Painter_BrushMode.PAINT ? cursorPaint : cursorDecal;
	}
	public void SetBrushSize(float newBrushSize){ //Sets the size of the cursor brush or decal
		brushSize = newBrushSize;
		brushCursor.transform.localScale = Vector3.one * brushSize;
	}

	////////////////// OPTIONAL METHODS //////////////////

	#if !UNITY_WEBPLAYER 
	/* 
		IEnumerator SaveTextureToFile(Texture2D savedTexture){		
			brushCounter=0;
			string fullPath=System.IO.Directory.GetCurrentDirectory()+"\\UserCanvas\\";
			System.DateTime date = System.DateTime.Now;
			string fileName = "CanvasTexture.png";
			if (!System.IO.Directory.Exists(fullPath))		
				System.IO.Directory.CreateDirectory(fullPath);
			var bytes = savedTexture.EncodeToPNG();
			System.IO.File.WriteAllBytes(fullPath+fileName, bytes);
			Debug.Log ("<color=orange>Saved Successfully!</color>"+fullPath+fileName);
			yield return null;
		}
		*/
	#endif

	/////////////add for leapmotion///////////////
    /** グーかどうか */
    bool IsRockGrip(List<Hand> hands)
    {
        return
            // 全ての指の内、開いている数が0個なら
            hands[dominatedHand].Fingers.ToArray().Count(x => x.IsExtended) == 0;
    }

    /** LeapのVectorからUnityのVector3に変換 */
    Ray handRay()
    {
		Ray returnRay = new Ray(transform.position, transform.forward);
        // -------- 右手の向いてる方向に飛ばす --------
        Vector3 rightAngle = rightHand.transform.right;
        Ray rightRay = new Ray(rightHand.transform.position, rightAngle);
        //Vector3 rightAngle = Quaternion.AngleAxis(15, new Vector3(0f, 1f, 0f)) * (rightHand.transform.right);
        // -------- 左手の向いてる方向に飛ばす --------
        Vector3 leftAngle = Quaternion.AngleAxis(15, new Vector3(0f, 0f, 0f)) * (-leftHand.transform.right);
        Ray leftRay = new Ray(leftHand.transform.position, leftAngle);

	
		List<Hand> hands = controller.Frame().Hands;
		Hand paintinghand = hands[dominatedHand];
		if(paintinghand.IsRight){
    	    Debug.DrawRay(rightHand.transform.position, rightAngle * 10, Color.red);
			returnRay = rightRay;
			ViewRay(rightHand.transform.position, rightHand.transform.position + rightAngle * 10);
		}else if(paintinghand.IsLeft){
    	    Debug.DrawRay(leftHand.transform.position, leftAngle * 10, Color.red);
			returnRay = leftRay;
			ViewRay(leftHand.transform.position, leftHand.transform.position + leftAngle * 10);
		}
		return returnRay;
    }

	void ViewRay(Vector3 startVec, Vector3 endVec){
        	lRend.SetVertexCount(2);
        	lRend.SetWidth (0.05f, 0.05f);
			lRend.SetPosition (0, startVec);
        	lRend.SetPosition (1, endVec);
	}
}
