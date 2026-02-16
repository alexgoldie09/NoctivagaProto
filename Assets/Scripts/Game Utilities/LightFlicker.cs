using UnityEngine;
using UnityEngine.Rendering.Universal;

public class LightFlicker : MonoBehaviour
{
    #region Variables
    enum LightFlickerType { simple, noise }

    [Header("Light References")]
    [SerializeField] Light2D torchLight;
    [SerializeField] Light2D backLight;

    [Header("Exposed Light Variables")]
    [Tooltip("The torch main light's intensity will take on the value set by this variable")]
    [SerializeField] float maxTorchIntensity = 20f;
    [Tooltip("The back light's intensity will take on the value set by this variable")]
    [SerializeField] float maxBackIntensity = 1f;
    [Tooltip("Max On Time is the maximum duration that the light can be 'On' for when in 'Simple' flicker mode.")]
    [SerializeField] float maxOnTime = 5f;
    [Tooltip("Max Off Time is the maximum duration that the light can be 'Off' for when in 'Simple' flicker mode.")]
    [SerializeField] float maxOffTime = 0.5f;
    [Tooltip("Min On Time is the minimum duration that the light can be 'On' for when in 'Simple' flicker mode.")]
    [SerializeField] float minOnTime = 1f;
    [Tooltip("Min Off Time is the minimum duration that the light can be 'off' for when in 'Simple' flicker mode.")]
    [SerializeField] float minOffTime = 0.1f;

    [Header("Noise Flicker References")]
    [Tooltip("This is where you will select the Flicker Mode. Simple is just turning off and on with the time constraints. Noise uses Perlin noise to flicker the light.")]
    [SerializeField] LightFlickerType lightFlickerType;
    [Tooltip("Frequency determines how fast the light will flicker.")]
    [SerializeField, Range(0f, 10f)] float frequency = 1f;
    [Tooltip("Bottom Cutoff determines at what % the light will go from dim to off instantly. Maximum 49%, Default 25%.")]
    [SerializeField, Range(0, 0.49f)] float bottomCutoff = 0.25f;
    [Tooltip("Top Cutoff determines at what % the light will go from dim to maximum intensity instantly. Minimum 50%, Default 75%.")]
    [SerializeField, Range(0.5f, 1)] float topCutoff = 0.75f;

    float intensitySeed;
    float randomOnTime = 5f;    // a float value representing how long the light must stay on for, set to a random value every time the light needs to turn on.
    float randomOffTime = 0.1f; // a float value representing how long the light must stay off for, set to a random value every time the light needs to turn off.
    float currentTime = 0;      // a float value that we increment every frame to count the time that has passed from when the light either turns on or off to measure against the randomOnTime or randomOffTime.
    bool isLightOn = true;      // a bool used for checking whether the light is on or off to then do the corresponding action.
    #endregion

    #region Initialisation
    void Start()
    {
        if (torchLight != null) 
            torchLight.intensity = maxTorchIntensity;

        if (backLight != null) 
            backLight.intensity = maxBackIntensity;

        intensitySeed = Random.Range(0f, 1000f); // Randomising where the seed starts on the Perlin noise to ensure that if you're using this script on multiple lights, you don't have identical flicker patterns.
        randomOffTime = maxOffTime;
        randomOnTime = maxOnTime;
    }
    #endregion

    void Update()
    {
        currentTime += Time.deltaTime; // incrementing currentTime with the time between each frame. Same as: currentTime = currentTime + Time.deltaTime;

        if (lightFlickerType == LightFlickerType.simple) 
            SimpleFlicker(); // simply checking which Flicker mode has been selected in the inspector so that the code runs the correct one.
        else 
            NoiseFlicker();
        
    }

    #region Simple Flicker
    void SimpleFlicker()
    {
        if (isLightOn)
        {
            if (currentTime >= randomOnTime) // checking if currentTime is greater than or equal to randomOnTime
            {
                randomOffTime = Random.Range(minOffTime, maxOffTime); // if it is, I'll set the randomOffTime
                ToggleLight();                                        // and then turn off the light
            }
        }
        else
        {
            if (currentTime >= randomOffTime) // checking if currentTime is greater than or equal to randomOffTime
            {
                randomOnTime = Random.Range(minOnTime, maxOnTime);  // if it is, I'll set the randomOnTime
                ToggleLight();                                      // and then turn on the light.
            }
        }
    }

    void ToggleLight() // Using this method as a toggle to prevent duplicate code written in the SimpleFlicker method.
    {
        currentTime = 0;        // resetting currentTime to 0 so that we can see how long the light spends in the new state.

        isLightOn = !isLightOn; // Simplified from 
                                // if (isLightOn) isLightOn = false ; 
                                // else isLightOn = true;

        if (torchLight != null)
            torchLight.intensity = isLightOn ? maxTorchIntensity : 2f;    // Simplified from
        
        if (backLight != null)
            backLight.intensity = isLightOn ? maxBackIntensity : 1f;     // if (isLightOn) pointLight.intensity = maxIntensity;
                                                                // else pointLight.intensity = 0;
    }
    #endregion

    #region Noise Flicker
    void NoiseFlicker()
    {
        float intensityTorchNoise = Mathf.PerlinNoise(intensitySeed, Time.time * frequency); // here I get intensityNoise as a value determined by the seed, time and the frequency that gets a value from the noise image.
                                                                                         // it effectively scrolls through the noise.
        
        float intensityBackNoise = intensityTorchNoise;
        
        float intensityTorch = maxTorchIntensity * intensityTorchNoise;
        float intensityBack = maxBackIntensity * intensityBackNoise;

        if (intensityTorch > maxTorchIntensity * topCutoff) 
            intensityTorch = maxTorchIntensity; // Here I handle the cutoff for when the light is bright enough, I simply just make it jump to max intensity for a bit of solid time.
        else if (intensityTorch < maxTorchIntensity * bottomCutoff) 
            intensityTorch = 2f;    // Here I handle the cutoff for when the light is dim enough, I simply just make it jump to 0 for a bit of solid off time.

        if (intensityBack > maxBackIntensity * topCutoff) 
            intensityBack = maxBackIntensity; 
        else if (intensityBack< maxBackIntensity * bottomCutoff) 
            intensityBack = 1f; 
        
        if (torchLight != null)
            torchLight.intensity = intensityTorch;
        
        if (backLight != null)
            backLight.intensity = intensityBack;
    }
    #endregion
}