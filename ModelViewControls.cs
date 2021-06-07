using Leap;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;

public class ModelViewControls : MonoBehaviour {
	private int yMinLimit = -80, yMaxLimit = 80;
	private Quaternion currentRotation, desiredRotation, rotation;
	private float yDeg=15, xDeg=0.0f;
	private float currentDistance,desiredDistance=3.0f,maxDistance = 10.0f,minDistance = 10.0f;
	private Vector3 position;
	public GameObject targetObject,camObject;
	float sensitivity=1.25f;
	/** LeapMotionのコントローラー */ 
	private Controller controller; 
	//Dropdownを格納する変数 
	[SerializeField] private Dropdown dropdown; 
    private int mouseHand = 0;
    Vector3 preHandPosition = Vector3.zero;

	void Start () {
		// LeapMotionのコントローラー
        controller = new Controller();
		// LeapMotionから手の情報を取得 
		var handsStream = this.UpdateAsObservable() 
			.Select(_ => controller.Frame().Hands);
       
        	// グー終了判定ストリーム
        var endRockGripStream = handsStream
            .Where(hands => !IsRockGrip(hands));
		currentDistance = Mathf.Clamp(desiredDistance, minDistance, maxDistance);
	}
	
	// Update is called once per frame
	void Update () {
		CameraControlUpdate ();
	}

	void CameraControlUpdate(){			
        bool check = false;
        List<Hand> hands = controller.Frame().Hands;
		if(hands.Count == 2){
			CheckHand();
            check = true;
		}else if(hands.Count == 1){
			if(dropdown.value == 1 && hands[0].IsRight || dropdown.value == 0 && hands[0].IsLeft){
				mouseHand = 0;
                check = true;
			}
        }
        if(check){
			Hand hand = controller.Frame().Hands[mouseHand];
			Vector palmPos = hand.PalmPosition;
			Vector3 handPosition = ToVector3(palmPos);
        	if (IsRockGrip(controller.Frame().Hands)){
        	    CameraPositionControl(handPosition); //カメラのローカル移動 キー
			}
				preHandPosition = handPosition;
        }
		yDeg = ClampAngle(yDeg, yMinLimit, yMaxLimit);		
		desiredRotation = Quaternion.Euler(yDeg, xDeg, 0);		
		rotation = Quaternion.Lerp(targetObject.transform.rotation, desiredRotation, 0.05f  );
		targetObject.transform.rotation = desiredRotation;
		desiredDistance = Mathf.Clamp(desiredDistance, minDistance, maxDistance);
		currentDistance = Mathf.Lerp(currentDistance, desiredDistance, 0.05f  );
		position = targetObject.transform.position - (rotation * Vector3.forward * currentDistance );
		Vector3 lerpedPos=Vector3.Lerp(camObject.transform.position,position,0.05f);
		camObject.transform.position = lerpedPos;

	}
	private static float ClampAngle(float angle, float min, float max)
	{
		if (angle < -360)
			angle += 360;
		if (angle > 360)
			angle -= 360;
		return Mathf.Clamp(angle, min, max);
	}

	void CheckHand(){
		int left = 0;
		int right = 1;
		List<Hand> hands = controller.Frame().Hands;
			Vector hand0 = hands[0].PalmPosition;
			Vector hand1 = hands[1].PalmPosition;
			if(hand1.x < hand0.x){
				left = 1;
				right = 0;
			}
			if(dropdown.value == 0){
				mouseHand = left;
			}else if(dropdown.value == 1){
				mouseHand = right;
			}
	}

    private void CameraPositionControl(Vector3 handPosition){
		float diff;
		
        if (handPosition.x > preHandPosition.x + 10) { 
			diff = handPosition.x - preHandPosition.x;
			xDeg+=diff;
        }//右に回転
        if (handPosition.x + 10 < preHandPosition.x) { 
			diff = handPosition.x - preHandPosition.x;
			xDeg+=diff;
        }//左に回転
        if (handPosition.y + 10 < preHandPosition.y) { 
			diff = preHandPosition.y - handPosition.y;
			yDeg+=diff;
        }//上に移動
        if (handPosition.y > preHandPosition.y + 10) { 
			diff = preHandPosition.y - handPosition.y;
			yDeg+=diff;
        }//下に移動
    }

    bool IsRockGrip(List<Hand> hands)
    {
        return
            // 全ての指の内、開いている数が0個なら
            hands[mouseHand].Fingers.ToArray().Count(x => x.IsExtended) == 0;
    }

    /** LeapのVectorからUnityのVector3に変換 */
    Vector3 ToVector3(Vector v)
    {
        return new Vector3(v.x, v.y, 0.0f);
    }
}