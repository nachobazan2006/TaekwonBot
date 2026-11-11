using Microsoft.EntityFrameworkCore;
using TaekwondoAPI.Models;

namespace TaekwondoAPI.Data;

public static class LocalDatabase
{
    public static string FindWorkspace(string start)
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, ".local", "settings.json"))) return directory.FullName;
        throw new InvalidOperationException("No se encontró configuración privada local. Ejecutá el lanzador desde el workspace.");
    }
    public static async Task SeedAsync(TaekwondoDbContext db, TimeProvider clock)
    {
        if (await db.AlumnosClub.AnyAsync()) return;
        var today = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires")).Date;
        var start = new DateTime(today.Year, today.Month, 1).AddMonths(-2);
        var students = new[]
        {
            new AlumnoClub { NombreApellido = "Alex Prueba", Graduacion = "Blanco", Horario = "Infantil A", FechaRegistro = start, FichaMedica = "Si", Socio = "Si" },
            new AlumnoClub { NombreApellido = "Alex Prueba", Graduacion = "Amarillo", Horario = "Adultos B", FechaRegistro = start.AddMonths(1), FichaMedica = "No", Socio = "No" },
            new AlumnoClub { NombreApellido = "Sol Ficticia", Graduacion = "Blanco", Horario = "Infantil A", FechaRegistro = start, FichaMedica = "Si", Socio = "No" },
            new AlumnoClub { NombreApellido = "Rio Ficticio", Graduacion = "Verde", Horario = "Adultos B", FechaRegistro = start, FichaMedica = "Si", Socio = "Si" },
            new AlumnoClub { NombreApellido = "Luz Ejemplo", Graduacion = "Azul", Horario = "Adultos B", FechaRegistro = start, FichaMedica = "Si", Socio = "Si" },
            new AlumnoClub { NombreApellido = "Mar Ejemplo", Graduacion = "Blanco", Horario = "Infantil A", FechaRegistro = start, FichaMedica = "Si", Socio = "No" }
        };
        db.AlumnosClub.AddRange(students);
        await db.SaveChangesAsync();
        db.Cuotas.Add(new Cuota { AlumnoClubId = students[0].Id, NombreApellido = students[0].NombreApellido, PeriodoAnio = start.Year, PeriodoMes = start.Month, Mes = System.Globalization.CultureInfo.GetCultureInfo("es-AR").DateTimeFormat.GetMonthName(start.Month), Monto = 10000, MedioPago = "Efectivo", Fecha = start.AddDays(1), Clase = students[0].Horario });
        db.Cuotas.Add(new Cuota { AlumnoClubId = students[2].Id, NombreApellido = students[2].NombreApellido, PeriodoAnio = today.Year, PeriodoMes = today.Month, Mes = System.Globalization.CultureInfo.GetCultureInfo("es-AR").DateTimeFormat.GetMonthName(today.Month), Monto = 0, MedioPago = "Beca", Fecha = today, Clase = students[2].Horario });
        await db.SaveChangesAsync();
    }
}
