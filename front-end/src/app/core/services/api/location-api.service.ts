import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { URLs } from '../../constants/URLs';
import { LocationCreateDTO } from '../../DTOs/location/location-create-dto';
import { LocationDTO } from '../../DTOs/location/location-dto';
import { LocationUpdateDTO } from '../../DTOs/location/location-update-dto';

@Injectable({ providedIn: 'root' })
export class LocationApiService {
  // DI
  private http = inject(HttpClient);

  // private
  private readonly url = URLs.baseApi + '/location';

  // methods
  public getAll() {
    return this.http.get<LocationDTO[]>(this.url);
  }

  public create(locationCreateDTO: LocationCreateDTO) {
    return this.http.post<LocationDTO>(this.url, locationCreateDTO);
  }

  public update(locationUpdateDTO: LocationUpdateDTO) {
    return this.http.put<LocationDTO>(this.url, locationUpdateDTO);
  }

  public delete(id: string) {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
