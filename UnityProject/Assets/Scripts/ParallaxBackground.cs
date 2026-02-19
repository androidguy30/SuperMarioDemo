using UnityEngine;

/// <summary>
/// Parallax scrolling for background elements (hills, clouds, bushes).
/// Attach to background sprite objects.
/// </summary>
public class ParallaxBackground : MonoBehaviour
{
    [SerializeField] private float parallaxFactor = 0.5f;
    [SerializeField] private bool infiniteHorizontal;
    [SerializeField] private float spriteWidth;

    private Transform cam;
    private float startX;
    private float startCamX;

    private void Start()
    {
        cam = Camera.main.transform;
        startX = transform.position.x;
        startCamX = cam.position.x;

        if (infiniteHorizontal && spriteWidth <= 0)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null) spriteWidth = sr.bounds.size.x;
        }
    }

    private void LateUpdate()
    {
        float deltaX = cam.position.x - startCamX;
        float parallaxX = startX + deltaX * parallaxFactor;

        transform.position = new Vector3(parallaxX, transform.position.y, transform.position.z);

        // Infinite scrolling
        if (infiniteHorizontal && spriteWidth > 0)
        {
            float camOffset = cam.position.x * (1 - parallaxFactor);
            if (camOffset > startX + spriteWidth)
            {
                startX += spriteWidth;
            }
            else if (camOffset < startX - spriteWidth)
            {
                startX -= spriteWidth;
            }
        }
    }
}
