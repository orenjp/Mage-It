using UnityEngine;
using System.Collections;

public class SceneManagement : MonoBehaviour {

	public Texture2D  fadeOutTexture; // the texture that will overlay the screen. this can be a black image or a loading graphic
	public float fadeSpeed = 0.8f;

	private int drawDepth = -1000; //the texture's order in the draw hierarchy: a low number means it renders on top
	private float alpha = 1.0f; //the texture's alpha value between 0 and 1
	private int fadeDir =-1; //the direction to fade: in = -1 or out = 1

	void OnGUI()
	{
		alpha += fadeDir * fadeSpeed * Time.deltaTime;	
		//force (clamp) the number between 0 and 1 because GUI>color uses alpha values between 0 and 1
		alpha = Mathf.Clamp01(alpha);
		// set color of our GUI (in this case our texture). All color values remain the same & the alpha is set to alpha variable
		GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, alpha); //set the alpha value
		GUI.depth = drawDepth; //make the black texture render on top (drawn last)
		GUI.DrawTexture (new Rect (0,0,Screen.width, Screen.height), fadeOutTexture); //draw the texture to fit the entire screen area 
	}

	//sets fadeDir to the direction parameter making the scene fade in if -1 and out if 1
	public float BeginFade (int direction)
	{
		fadeDir = direction;
		return fadeSpeed;
	}
	
	// OnLevelWasLoaded is called when a level is loaded. 
	void OnLevelWasLoaded()
	{
		BeginFade(-1); // call the fade in function
	}
    public IEnumerator LoadScene(string sceneName)
    {
        float fadeTime = BeginFade(1);
        yield return new WaitForSeconds(fadeTime);
        Application.LoadLevel(sceneName);
    }
}
