/**
 * The paging query of the lazy endpoints. The backend LazyDTO calls these `Skip` and `Take`;
 * the api services map `taken` to `skip` and `sectionSize` to `take` when they build the request.
 */
export interface LazyDTO {
  /** how many items are already taken (loaded), sent to the backend as `skip` */
  taken: number;
  /** how many items one section (page) holds, sent to the backend as `take` */
  sectionSize: number;
}
