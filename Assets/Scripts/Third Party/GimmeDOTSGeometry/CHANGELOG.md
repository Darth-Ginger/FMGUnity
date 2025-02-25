=== Gimme DOTS Geometry ===

# Changelog

## [2.0.6] - 2024-16-12

## Added

- DBSCAN 2D

## Fixes

- Fixed a problem with KD-Tree queries introduced in the last update. Performance stays faster and the problem
  is fixed


## [2.0.5] - 2024-23-11

## Added

- Ball*- and R*- Trees now support Nearest Neighbor Queries!
- R* Tree now supports Frustum Culling (previously only Ball* Tree supported it)
- Native QuickSelect Algorithm
- UV Capsule Mesh Generation

## Improvements

- Improved KD-Tree Construction Speed significantly
- Improved KD-Tree Query Performance for large data sets



## [2.0.4] - 2024-11-11

## Added

- Concave Hull Algorithm

## Fixes

- Removed NotBurstCompatible Attribute causing an error message in Unity 6



## [2.0.3] - 2024-11-05

Warning: KD-Tree API has changed significantly in this update!

## Added

- Added Native Red-Black Tree

## Improvements

- Improved KD-Tree API - making it more adaptable (you can now use custom structs)
- Improved AVL-Tree performance slightly
- Improved Monotone-Y Triangulation Performance by about 20-40%
- Reduced Delaunay and Voronoi Memory usage and improved performance by about 5-10%




## [2.0.2] - 2024-05-26

## Added

- Added ECS Example Scenes
- Line Segment - Triangle Intersection

## Fixes

- Method for removing colinear vertices from a polygon that has holes is working now



## [2.0.1] - 2024-04-27

## Added

- UV Sphere Mesh Primitive

## Improvements

- Native Polygon Handle now supports Undo
- Improved Ear-Clipping Triangulation Performance by about 40% (Prefiltering point-checks)

## Fixes

- Fixed R* and Ball*-Tree Remove-methods not being usable in a Job
- Fixed an issue with mesh slicing in newer version of the collections package (a method
  was deleted from the package)



## [2.0.0] - 2024-03-27

## !Important!

The minimum Unity Version required is now 2021.3!
Do not update if you are using an older version!

## Added

- Mesh Slicing!
- Added Parallel Line-Plane Intersection Jobs and Scene
- Added 3D Grids Mesh Primitive (previously only 2D was available)
- Capsule Overlap / "Sphere Cast" for Ball* Trees

## Improvements

- Improved 3D KD-Tree Radius Query Performance by removing Bounds-structs in an internal job (~15% better)
- Added an optional presorted mode to All Radius and All Rectangle Queries, that can further improve
  performance
- Increased Ball* Tree and R* Tree Raycast performance (more efficient sorting and comparison)

## Fixes

- Fixed an error in Nearest Neighbor search in KD-Trees due to a left-right weakness on my part
- Fixed an invalid deallocation in the Y-Monotone Triangulation algorithm
- Queries of 3D Ball* Trees could return invalid results because of an error introduced while optimizing
  in a previous version
- Added a variant to Delaunay that preserves the order of the initial input (which may have caused
  some issues or not depending on how you used the algorithm)


## [1.5.3] - 2024-02-02

## Added

- KD-Tree Nearest Neighbor Search Jobs (2D + 3D)

## Improvements

- Editor Polygon Handles now work in each plane (XZ-, XY- and YZ)
- Reduced memory usage of KD-Trees
- Removed the manual disposing of Job Allocations in many cases. This changed
  the API in some places, so be careful when upgrading!

Note: This update has many changes to the API,
      so make sure to take a look at the Upgrade Guide!


## [1.5.2] - 2024-01-29

## Added

- Ball* Tree Frustum Query
- Dynamic R* Trees (2D + 3D)
- Prism

## Improvements

- Improved Ball* Tree position update time by about 30%

## [1.5.1] - 2024-01-23

## Added

- All Rectangle Query
- All Radius and All Rectangle Parallel Queries
- Overlap Queries for Ball* Trees
- Plane Primitive

## Improvements

- Improved All Radius Query Performance by about 10%
- Improved performance of all trees slightly by consistently using the radius squared
  (some methods did not use it)

## Fixes

- Fixed a small NativeSortedList Bug when removing an element from
  a list of length 1 when the element was not contained

## [1.5.0] - 2024-01-18

## Added

- Delaunay Triangulation
- Voronoi Diagrams
- Polygon Outline Primitive
- Cone Primitive

## Fixes

- Fixed an unormalized vector in 2D Arrow mesh creation

## [1.4.4] - 2023-12-17

## Added

- All Radius Query
- Polygon Queries for 2D KD-Tree and 2D Ball* Tree

## Fixes

- Fixed IsCreated being false, when a NativeSortedList was empty
- Fixed IEnumerator Exception when iterating through an empty NativeSortedList
- Fixed some unnormalized vectors in a mesh generation method
- Removed all [BurstCompatible] attributes as they were deprecated

## [1.4.3] - 2023-12-02

## Added

- Added NativeSortedList (Skip List)
- Added multiple mesh-generation methods (Arrows, Torus, Tetrahedron, Cylinder,  etc.)


## [1.4.2] - 2023-11-24

## Added

- Added Area()-Method for NativePolygon2D
- Added Editor Settings for Degenerate Cases and Safety Handling
- Added an =<Upgrade Guide>= (because of changes to the API)

## Improvements

- Added optional safety checks to many methods to signal problems with the input to the user
- Replaced NativeLists of NativePolygons with UnsafeLists, so the polygons can be put inside
  NativeContainers
- Improved Performance of Point Location Jobs for Polygons by approximately 30%
- Improved Akl-Toussaint Heuristic Performance by approximately 5%
- Improved Convex-Hull Algorithm Performance by approximately 30%

## Fixes

- Fixed Y-Monotone Chain Algorithm not sorting left and right correctly when having collinear vertices
  (more stable triangulation with collinear vertices)
- Fixed Polygon IsConvex()-Check also checking if holes were convex (which was confusing)
- Removed Jobs Package as a dependency (deprecated)



## [1.4.1] - 2023-11-13

## Added

- Added Minimum Enclosing Disc
- Added Minimum Enclosing Sphere

## Fixes

- Fixed Count-Property of Quadtrees / Octrees not working properly when using regular methods in a custom job


## [1.4.0] - 2023-11-03

## Added

- Added 2D Ball* Tree
- Added 3D Ball* Tree


## [1.3.2] - 2023-10-20

## Added

- Added Parallel Queries to Quadtrees, Octrees and KD-Trees

## [1.3.1] - 2023-09-20

## Fixed

- Fixed radius search queries, when the radius was much smaller than the size of a cell. Fix also
  improves search performance slightly (a little bit faster now)


## [1.3.0] - 2023-09-19

## Added

- Dense Quadtrees
- Dense Octrees

## Fixed

- Removed a small inaccuracy for radius search queries in quadtrees and octrees

## Improvements

- Added IEquatable Type Constraint to Quadtrees and Octrees, 
  so that Remove() and Update() methods can be used in Jobs

## Changes

- Renamed NativeSpatialQuadtree and NativeSpatialOctree to NativeSparseQuadtree and NativeSparseOctree
  (both versions, sparse and dense use spatial-hashing)



## [1.2.2] - 2023-08-26

## Improvements

- Added Count to Quadtrees / Octrees
- Entries in Quadtrees and Octrees are not updated anymore when they would still point to the same bucket (improved update performance)

## Fixed

- Fixed an issue with NativeMultiHashMap in Quadtrees/ Octrees when the capacity was exceeded (probably caused by hash collisions)
- Fixed warning because of implicit casts in the samples code (I forgot in the last version)



## [1.2.1] - 2023-06-14

## Fixes

- Fixed warnings because of implicit cast, introduced in the collections package version [2.1.0-exp.4]



## [1.2.0] - 2023-06-08

### Added

- Added Native3DKDTree



## [1.1.0] - 2023-05-08

### Added

- Added Native2DKDTree (works in XY, XZ and YZ, for fast range queries with a static dataset)

### Fixes

- Fixed a drawing error within the Octree Handle



## [1.0.1] - 2023-04-07

### Added

- Added NativePolygon2DSampler (enables you to generate points distributed in a polygon)
- Added Update/Clear methods to Quadtree and Octree

### Fixes

- Fixed NativePolygon2D.Distance when returning signed values



## [1.0.0] - 2022-12-14

### Added

- Initial Commit