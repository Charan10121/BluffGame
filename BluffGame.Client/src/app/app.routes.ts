import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'lobby',
    loadComponent: () =>
      import('./components/lobby/lobby.component').then(m => m.LobbyComponent)
  },
  {
    path: 'room/:id',
    loadComponent: () =>
      import('./components/room/room.component').then(m => m.RoomComponent)
  },
  { path: '', redirectTo: 'lobby', pathMatch: 'full' },
  { path: '**', redirectTo: 'lobby' }
];
