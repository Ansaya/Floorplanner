# FPGA Resources Floorplanner
This software has been designed for an FPGA floorplanning contest as a project during an Heterogeneous Systems Architecture course at Politecnico di Milano.

## Floorplan algorithm overview
The basic idea behind the software is to have some regions, requiring a given amount of resources, to be placed in a given FPGA layout.
The first step is to find a suitable valid placement for all regions, so a basic non-optimized floorplan; now, given that at least a feasible placement has been found, an optimization phase begins.

The optimization is organized in subsequent iterations, each one tries to visit a different solution space moving and replacing some regions: when a better placement is found, it is taken as starting point for next optimization iterations and so on.
This phase is the main algorithm step and takes most of the time, but has prooven to be quite effecctive, in particular with larger FPGA layouts.

## Feasible placement search
First algorithm stage is a simple trial and error phase during which regions are ordered and placed randomly inside the FPGA layout until a valid placement is found. If the FPGA runs out of available resources when there are still some regions to place, current placing is partially disrupted and the process continues.

This phase should find a starting regions floorplan to be optimized, if this doesn't happen the algorithm stops.

## Floorplan optimization
This is main algorithm stage: optimization iterations are performed over current best solution.
An iteration consist of the current layout being partially disrupted trying to find a better placement for highest cost regions; this is achieved applying different disruption and placement policies combined together evry time.

Each iteration consists of a work pool (typically one per core) where each worker is optimizing a different region. When all concurrent worker have reached a new solution or a dead end, new floorplans are compared, current best is updated if necessary and next iteration step is fired.

## Final floorplan
The algorithm stops when reaching the maximum number of iterations giving current best found floorplan.
Of course all search parameters can be tuned from command line before the execution starts: iteration number, partial disruption magnitude, minimum region dimensions, number of iterations and workers per iteration. Tuning depends on many factors such as FPGA available/required resources ratio and regions dimensions and can be determinant to achieve a good floorplan.
