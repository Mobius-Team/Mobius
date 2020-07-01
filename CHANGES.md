## Beta 1.4.2
\+ Added support for the additional info on the top scripts floater   
\* Updated prebuild to support vs2019   
\* Updated .gitignore to core's   
\* Fixed a minor issue with os_groups_Store.migrations   

### Beta 1.4.2 Notes:
 - I haven't done the additional filters yet for Top Scripts   

## Beta 1.4.1
\+ Added PARCEL_DETAILS_TELEPORT_ROUTING   
\+ Added PARCEL_DETAILS_OBJECT_RETURN    
\+ Added commands to add/remove ip and hardware bans   
\+ Added a few terrain commands to the inworld region console   
\* Updated the ini examples to include the new access service   
\* Changed the solution name to Mobius   

### Beta 1.4.1 Notes:
 - The inworld region console now allows estate managers to elevate, lower, and fill the region's terrain. I've also added a new command for loading the terrain using a texture uuid of an uploaded height map.

## Beta 1.4
\+ Added osTriggerSoundAtPos  
\+ Added PARCEL_DETAILS_LANDING_POINT   
\+ Added a basic hardware/ip banning service    
\* Fixed mistakes in the region restart module   
\* Removed jOpenSim    
\* Merge with osCore2 (8bc51b)   

## Beta 1.3
\+ Added support for Abuse Reports   
\* Corrected a mistake in the auth migration   
\* Merge with osCore2 (433a2fa)   

### Beta 1.3 Notes:
 - Like with Display Names, your AbuseReportsService ConnectionString will need to include `CharSet=utf8mb4;` in order to use special characters.

## Beta 1.2
\+ Added RSA Login Support   
\+ Added TOS Support   
\* Resident is now trimmed from the username of login messages   
\* Merge with osCore2 (8ad04c3)   

### Beta 1.2 Notes:
 - RSA Logins are a work in progress and currently only support PEM format for public/private keys. In the future I would like to add more supported formats.

## Beta 1.1.1
\* Fixed some null reference nonsense for GridUsers and Display Names   
\* Updated OSAWS submodule   
\* Merge with osCore2 (e385956)   

## Beta 1.1
\+ New Region Restart Notification   
\+ LSLSyntax Module   
\+ ViewerAsset Module   
\+ External AvatarPickerSearch Handler   
\+ Port range settings on simulators   
\+ Option to have the region's port match the port of the sim   
\* Merge with osCore2 (2766eef)

#### Beta 1.1 Notes:
 - The matching port setting will only work for one region per simulator.
 - A PHP handler for AvatarPickerSearch and ViewerAsset will be included in OSAWS.
 - I had to remove core's implementation of LSLSyntax and ViewerAsset.
 - The region restart plugin has been changed to take the amount of time until a restart instead of a list. The RemoteAdminPlugin has been changed to match this behaviour.
 - To make a region use the same port as the simulator, set its `InternalPort` to `MATCHING` in the ini.

## Beta 1.0.1
\+ New ROBUST service to provide display names more efficiently  
\* `FetchDisplayNamesInterval` is now `DisplayNamesCacheExpirationInHours` with a default value of 12   
\* HG display name updates are now sorted by their home grid and a single request is made to each grid
#### Beta 1.0.1 Notes:
 - Don't forget to add the HGDisplayNameServiceConnector to your ROBUST config so other grids can fetch your display names!

## Beta 1.0
\+ Display Names  
\+ Option to hide the last name Resident  
\+ No last name login for users with the lastname Resident  
#### Beta 1.0 Notes:
 - I've only coded the MySQL parts of Display Names so far.
 - The `CharSet=utf8mb4;` specified in the MySQL ConnectionStrings of UserAccountService and GridUserService is required.
 - Fetch Display Names still needs work as it's not grouping requests properly, and the timeout is far too big.
 - LSL Display Name functions have only been updated in YEngine.
 - Name Tags will appear to the user as they would appear on the user's home grid.
 - I'm trying out a thing where where HG usernames are displayed as `first.last@grid.url.com` instead of `first.last.@grid.url.com` for nametags. This doesn't affect how it is formatted in other places.
 - I'm also trying a thing where HG visitors will have their proper name as their display name if they don't already have a display name.
 - The 7 day wait for changing your display name is hardcoded at the moment as the viewer will always say 7 days.
 - Display names of HG visitors are updated when they arrive.
