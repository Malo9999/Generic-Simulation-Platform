# Future Simulation Ideas

These simulations are potential future modules once the Season-1 pipeline is validated.  
They follow the platform philosophy of **simple primitives + emergence + replayable runs**.

---

## 1. Particle Life Ecosystems

Particles interact via an attraction/repulsion matrix.

Each species defines how it reacts to other species.

Emergent behavior:

- cell-like clusters
- predator-prey dynamics
- orbiting swarms
- ecosystem-like structures

Core mechanics:

- position
- velocity
- species id
- interaction matrix

Why interesting:
Extremely simple implementation but produces complex, life-like patterns.

---

## 2. Slime Mold Network Builder

Inspired by *Physarum polycephalum*.

Agents deposit pheromone trails while exploring.

Environment rules:

- trail diffusion
- trail decay

Emergent behavior:

- transport networks
- shortest paths
- branching vein structures

Possible experiments:

- connect cities
- solve maze networks
- simulate transport infrastructure.

---

## 3. Boids Ecosystem (Predator–Prey)

Based on flocking rules.

Movement primitives:

- separation
- alignment
- cohesion

Combined with:

- predators hunting prey
- food resources
- energy systems
- reproduction with mutation

Emergent results:

- herds
- hunting strategies
- population cycles
- evolutionary pressure.

---

## 4. Reaction–Diffusion Systems

Chemical pattern formation using two interacting fields.

Rules:

- diffusion
- reaction rates
- decay

Produces:

- zebra stripes
- leopard spots
- coral-like patterns
- alien skin textures.

Useful as a procedural pattern simulation.

---

## 5. Gray–Scott Pattern Life

A specific reaction–diffusion model producing dynamic structures.

Emergent structures:

- worm-like shapes
- dividing organisms
- moving wave patterns

Appears like microscopic alien life.

---

## 6. Digital Galaxy Formation

Particles simulate simplified gravitational attraction.

Rules:

- long-range attraction
- short-range repulsion
- velocity damping

Emergent results:

- spiral galaxies
- star clusters
- orbital rings.

---

## 7. Terrain-Carving Agents

Agents modify terrain while moving.

Rules:

- dig terrain
- carry material
- deposit elsewhere

Emergent behavior:

- caves
- tunnels
- dune migration
- landscape formation.

---

## 8. Information Propagation Swarms

Agents communicate through signal fields instead of pheromones.

Signals:

- danger
- food discovery
- migration direction

Emergent results:

- coordinated movement
- panic waves
- swarm intelligence.

---

## 9. Artificial Civilizations

Agents gather resources and build settlements.

Resources:

- food
- wood
- stone

Emergent results:

- villages
- trade routes
- territorial expansion
- conflicts.

---

## 10. Diffusion-Limited Aggregation

Particles perform random walks until they stick to clusters.

Produces fractal structures resembling:

- lightning
- crystals
- coral growth
- snowflake formation.

Extremely simple algorithm with visually striking results.

---

## Notes

These simulations are not part of the Season-1 implementation roadmap.

They are candidates for **Season-2 experimental simulations** once the core platform pipeline is proven:

Scenario → Run → Logs → Replay → Video.

Non-goals:

- Do not implement any of these simulations yet.
- Do not modify existing Simulation modules.
