If you are here for ReMaster Utilities executable, there are 3 here.  Please use the most recent upload if you want the most current version.  
-- note, .Net 3.5 version for Windows XP will no longer be receiving updates due to compiler errors on the more recent code.

Protection methods currently handled by ReMaster include..

V-Max (all versions)
RapidLok
Vorpal (EPYX -- California Games, Legend of Blacksilver, Wrestling, The Games - Summer/Winter Edition)
Pirate Slayer (EA)
Fat Tracks (EA/Activision)
Micro Prose (custom format)
Cyan Loader
Radwar
Rainbow Arts/Magic Bytes
GMA/Securispeed
.. and some images with special signatures on the inner tracks.

Note: 

This is NOT a 100% replacement for Nibconv.exe.  There are many protections that this tool can't handle properly but are handled by Nibconv, and as such, I have no intention of implementing
them into ReMaster (currently).

ReMaster Utility is a Windows based utility for repairing (fixing) heavily copy-protected NIB images and converts to re-writeable G64 files for c64 emulation or for use on real c64 hardware.

Some c64 games use very short synchronization signals that sometimes don't get picked up by the 1541/71 disk drive when reading from original disks and if these sync signals aren't the correct
length or the data becomes bit-shifted from improper framing, the image will not work when in emulators or when written back to disk.  ReMaster Utility will analyze V-Max and Vorpal protected
NIB images and correct the syncing lengths and/or re-frame the track data to it's proper orientation and output a G64 image that will work in an emulator or when written back to disk for use
on a real c64 with 1541/71 disk drive
