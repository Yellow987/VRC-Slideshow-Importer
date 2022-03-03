# VRC Slideshow-Importer
Bring Slideshow or PDF into VRChat with slide/page toggles easily !!
Just drag and drop.

## Example slideshow

https://user-images.githubusercontent.com/29646293/156586525-5772f3f5-a523-436f-a5fd-1d480cd816f7.mp4


## Notes
This tool is parameter optimized and takes only 3 memory on your VRC synced parameter controller for a slideshow of N slides.
The slideshow due to it's large bounding box size will make your avatar performance rating "Very Poor". Simply edit the scale of the slides object in the Resources folder to be under 2.5 on the X-axis to meet "Poor" avatar performance ranking.

## How to use
When you import the unity package you will see the YellowTools slideshow generator at the top. Click to open the GUI.

![GUI](https://user-images.githubusercontent.com/29646293/156586614-f59491ea-2993-4de4-ab23-82781f95c072.png)

The slides that this takes is a single png of all the slides. This can be generated from a google slideshow through:
1) Converting the slideshow to pdf(Google allows slideshow download to pdf.
2) Converting the slideshow to a folder of images(See websites like: https://pdf2png.com/ )
3) Merging the pngs into a single verticle png(See websites like: https://photo333.com/merge-png.php)

It is recommended to attach the slideshow to a world-constrained object for a proper presentation. This can be gotten from: https://vrlabs.dev/item/world-constraint. 

![world-constraint](https://user-images.githubusercontent.com/29646293/156586649-7bf0cda2-b30d-4a57-8a9c-a7303006038f.png)

Drag and drop the parent container as the object to attach to.

Then generate the slideshow. Parameters and Menu interactions will automatically be added to your avatar's controllers.
Vioala!
![Transitions](https://user-images.githubusercontent.com/29646293/156587050-3e06a63e-c321-43c8-8c78-bbc83d6f8eb8.png)
