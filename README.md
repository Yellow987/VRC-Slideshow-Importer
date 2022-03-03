# VRC Slideshow-Importer
Bring Slideshow or PDF into VRChat with slide/page toggles easily !!
Just drag and drop.

## Example slideshow


## Notes
This tool is parameter optimized and takes only 3 memory on your VRC synced parameter controller for a slideshow of N slides.
The slideshow due to it's large bounding box size will make your avatar performance rating "Very Poor". Simply edit the scale of the slides object in the Resources folder to be under 2.5 on the X-axis to meet "Poor" avatar performance ranking.

## How to use
When you import the unity package you will see the YellowTools slideshow generator at the top. Click to open the GUI.
<img src="images/gui.png">
The slides that this takes is a single png of all the slides. This can be generated from a google slideshow through:
1) Converting the slideshow to pdf(Google allows slideshow download to pdf.
2) Converting the slideshow to a folder of images(See websites like: https://pdf2png.com/ )
3) Merging the pngs into a single verticle png(See websites like: https://photo333.com/merge-png.php)

It is recommended to attach the slideshow to a world-constrained object for a proper presentation. This can be gotten from: https://vrlabs.dev/item/world-constraint. 
<img src="images/example-data.png" >
Drag and drop the parent container as the object to attach to.

Then generate the slideshow. Parameters and Menu interactions will automatically be added to your avatar's controllers.
Vioala!
<img src="images/transitions.png">
